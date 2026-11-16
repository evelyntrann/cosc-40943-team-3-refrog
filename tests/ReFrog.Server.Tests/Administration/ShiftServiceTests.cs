using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Info.Persistence;
using ReFrog.Server.Persistence;
using ReFrog.Server.Tests.Info;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Administration;

// ADM.md test row "ADM/VOL shared shift contract" and VOL.md's ShiftService contract. Sample data only.
[TestClass]
public sealed class ShiftServiceTests
{
    private static readonly DateTimeOffset Now = new(2027, 5, 4, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task SignupCandidatesAreTheCurrentEventsShiftsThatHaveNotStarted()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var current = EventConfigServiceTests.Event(ReFrogEventStatus.Active, 2027);
        var old = EventConfigServiceTests.Event(ReFrogEventStatus.Closed, 2026);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.AddRange(current, old);
            return db.SaveChangesAsync();
        });
        var started = Shift(current.Id, Now.AddHours(-1));
        var upcoming = Shift(current.Id, Now.AddHours(20));
        var later = Shift(current.Id, Now.AddDays(2));
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.AddRange(later, started, upcoming, Shift(old.Id, Now.AddDays(1)));
            return db.SaveChangesAsync();
        });

        var candidates = await WithShiftService(host, service => service.FindSignupCandidatesAsync(Now, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { upcoming.Id, later.Id }, candidates.Select(x => x.ShiftId).ToArray());
        Assert.IsTrue(candidates.All(x => x.EventId == current.Id && x.EventStatus == ReFrogEventStatus.Active));
        Assert.AreEqual(upcoming.StartsAt, candidates[0].StartsAt);
        Assert.AreEqual(TimeSpan.Zero, candidates[0].StartsAt.Offset);
        Assert.AreEqual(TimeSpan.Zero, candidates[0].EndsAt.Offset);
    }

    [TestMethod]
    public async Task NoCurrentEventMeansNoSignupCandidates()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var old = EventConfigServiceTests.Event(ReFrogEventStatus.Closed, 2027);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(old);
            return db.SaveChangesAsync();
        });
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(Shift(old.Id, Now.AddDays(1)));
            return db.SaveChangesAsync();
        });

        Assert.IsEmpty(await WithShiftService(host, service => service.FindSignupCandidatesAsync(Now, CancellationToken.None)));
    }

    [TestMethod]
    public async Task FindByIdCarriesItsEventsStatusAndUnknownIdIsNull()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var old = EventConfigServiceTests.Event(ReFrogEventStatus.Closed, 2026);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(old);
            return db.SaveChangesAsync();
        });
        var shift = Shift(old.Id, Now.AddDays(-300));
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(shift);
            return db.SaveChangesAsync();
        });

        var found = await WithShiftService(host, service => service.FindByIdAsync(shift.Id, CancellationToken.None));
        var missing = await WithShiftService(host, service => service.FindByIdAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.AreEqual(ReFrogEventStatus.Closed, found?.EventStatus);
        Assert.AreEqual(shift.RequiredCount, found?.RequiredCount);
        Assert.IsNull(missing);
    }

    [TestMethod]
    public async Task LockJoinsAnotherContextsTransactionAndHoldsTheRowUntilItCommits()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var current = EventConfigServiceTests.Event(ReFrogEventStatus.Active, 2027);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(current);
            return db.SaveChangesAsync();
        });
        var shift = Shift(current.Id, Now.AddDays(1));
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(shift);
            return db.SaveChangesAsync();
        });

        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            // Stands in for VOL: its own EF Core context on the scope's shared connection opens the transaction.
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlUnitOfWork>();
            await using var volunteer = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>()
                .UseSqlServer(unitOfWork.Connection).Options);
            await using var transaction = await unitOfWork.BeginAsync(CancellationToken.None);
            await unitOfWork.EnlistAsync(volunteer, CancellationToken.None);
            Assert.AreEqual(1, await volunteer.Database.SqlQueryRaw<int>("SELECT @@TRANCOUNT AS [Value]").SingleAsync());

            var locked = await scope.ServiceProvider.GetRequiredService<ShiftService>()
                .LockForAssignmentAsync(shift.Id, CancellationToken.None);

            Assert.AreEqual(shift.Id, locked?.ShiftId);
            Assert.AreEqual(TimeSpan.Zero, locked?.StartsAt.Offset);
            var blocked = await Assert.ThrowsExactlyAsync<SqlException>(() => ChangeCapacityAsync(host, shift.Id, 5));
            Assert.AreEqual(1222, blocked.Number, "A concurrent capacity change must wait for the lock.");

            await transaction.CommitAsync(CancellationToken.None);
        }

        await ChangeCapacityAsync(host, shift.Id, 5);
        var stored = await host.WithAsync<AdmDbContext, int>(db =>
            db.Shifts.Where(x => x.Id == shift.Id).Select(x => x.RequiredCount).SingleAsync());
        Assert.AreEqual(5, stored);
    }

    [TestMethod]
    public async Task LockWithoutAnOpenSharedTransactionIsRefused()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            WithShiftService(host, service => service.LockForAssignmentAsync(Guid.NewGuid(), CancellationToken.None)));
    }

    internal static Shift Shift(Guid eventId, DateTimeOffset startsAt, int requiredCount = 2) => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        LocationId = Guid.NewGuid(),
        StartsAt = startsAt,
        EndsAt = startsAt.AddHours(3),
        RequiredCount = requiredCount,
        CreateRequestId = Guid.NewGuid(),
        CreatedByAccountId = Guid.NewGuid(),
        CreatedAt = Now.AddDays(-30),
    };

    private static async Task ChangeCapacityAsync(SqlTestHost host, Guid shiftId, int count)
    {
        await using var connection = new SqlConnection(host.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SET LOCK_TIMEOUT 1000; UPDATE [adm].[Shifts] SET [RequiredCount] = @count WHERE [Id] = @id";
        command.Parameters.AddWithValue("@count", count);
        command.Parameters.AddWithValue("@id", shiftId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> WithShiftService<T>(SqlTestHost host, Func<ShiftService, Task<T>> action)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ShiftService>());
    }

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);
}
