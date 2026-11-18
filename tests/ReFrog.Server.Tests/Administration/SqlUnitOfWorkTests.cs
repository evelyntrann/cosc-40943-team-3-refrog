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

// The shared transaction seam VOL builds on (ADM.md, SqlUnitOfWork): reads inside an open transaction, a
// transaction a caller forgets to end, and synchronous scopes. Sample data only.
[TestClass]
public sealed class SqlUnitOfWorkTests
{
    private static readonly DateTimeOffset Now = new(2027, 5, 4, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ShiftReadsWorkInsideAnotherContextsOpenTransaction()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlUnitOfWork>();
        await using var volunteer = Probe(unitOfWork);
        await using var transaction = await unitOfWork.BeginAsync(CancellationToken.None);
        await unitOfWork.EnlistAsync(volunteer, CancellationToken.None);
        var shifts = scope.ServiceProvider.GetRequiredService<ShiftService>();

        // VOL may read before it locks; the order of calls must not matter.
        var found = await shifts.FindByIdAsync(shift.Id, CancellationToken.None);
        var candidates = await shifts.FindSignupCandidatesAsync(Now, CancellationToken.None);
        var locked = await shifts.LockForAssignmentAsync(shift.Id, CancellationToken.None);

        Assert.AreEqual(shift.Id, found?.ShiftId);
        Assert.AreEqual(shift.Id, candidates.Single().ShiftId);
        Assert.AreEqual(shift.Id, locked?.ShiftId);
        await transaction.CommitAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task ATransactionLeftOpenIsRolledBackAndItsLockReleasedWhenTheScopeEnds()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlUnitOfWork>();
            await unitOfWork.BeginAsync(CancellationToken.None); // never committed or disposed by the caller
            await scope.ServiceProvider.GetRequiredService<ShiftService>().LockForAssignmentAsync(shift.Id, CancellationToken.None);
            var volunteer = Probe(unitOfWork);
            await unitOfWork.EnlistAsync(volunteer, CancellationToken.None);
            await volunteer.DisposeAsync();
        }

        // Ending the scope did not throw, and the row is free again: an update with a short lock wait succeeds.
        await using var connection = new SqlConnection(host.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SET LOCK_TIMEOUT 1000; UPDATE [adm].[Shifts] SET [RequiredCount] = 7 WHERE [Id] = @id";
        command.Parameters.AddWithValue("@id", shift.Id);
        Assert.AreEqual(1, await command.ExecuteNonQueryAsync());
    }

    [TestMethod]
    public async Task ASynchronousScopeCanUseAndDisposeTheShiftServices()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        using (var scope = host.Factory.Services.CreateScope())
        {
            var found = scope.ServiceProvider.GetRequiredService<ShiftService>()
                .FindByIdAsync(shift.Id, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(shift.Id, found?.ShiftId);
        }
    }

    private static async Task<Shift> SeedShiftAsync(SqlTestHost host)
    {
        var current = EventConfigServiceTests.Event(ReFrogEventStatus.Active, 2027);
        await host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(current);
            return db.SaveChangesAsync();
        });
        var shift = ShiftServiceTests.Shift(current.Id, Now.AddDays(1));
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(shift);
            return db.SaveChangesAsync();
        });
        return shift;
    }

    private static ProbeContext Probe(SqlUnitOfWork unitOfWork) =>
        new(new DbContextOptionsBuilder<ProbeContext>().UseSqlServer(unitOfWork.Connection).Options);

    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);
}
