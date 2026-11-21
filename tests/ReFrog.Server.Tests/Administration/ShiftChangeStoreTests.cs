using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Volunteer;
using ReFrog.Server.Features.Volunteer.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Administration;

// The locked change behind UC-ADM-manage-shifts 6a (ADM.md key decision 8; §8.2.8, §8.2.9). Sample data only.
[TestClass]
public sealed class ShiftChangeStoreTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task AnUpdateSavesTheDetailsAndItsAuditEntryAndGivesANewVersion()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);
        var version = Convert.ToHexString(shift.Version);
        var location = Guid.NewGuid();
        var startsAt = shift.StartsAt.AddHours(1);

        var result = await WithScopeAsync(host, (store, audit, _) => store.ChangeUnderLockAsync(shift.Id, version, ct =>
        {
            audit.Record(Guid.NewGuid(), "SHIFT_CHANGED", "Shift", shift.Id.ToString());
            return Task.FromResult<ShiftChangeDecision>(new ShiftUpdate(location, startsAt, startsAt.AddHours(2), 5));
        }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.Saved, result.Outcome);
        Assert.AreNotEqual(version, result.Shift?.Version);
        var stored = await StoredAsync(host, shift.Id);
        Assert.AreEqual(location, stored?.LocationId);
        Assert.AreEqual(startsAt, stored?.StartsAt);
        Assert.AreEqual(5, stored?.RequiredCount);
        Assert.AreEqual(Convert.ToHexString(stored!.Version), result.Shift?.Version);
        Assert.AreEqual("SHIFT_CHANGED", (await AuditActionsAsync(host)).Single());
    }

    [TestMethod]
    public async Task ARemovalDeletesTheShiftWithItsAuditEntry()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        var result = await WithScopeAsync(host, (store, audit, _) => store.ChangeUnderLockAsync(
            shift.Id, Convert.ToHexString(shift.Version), ct =>
            {
                audit.Record(Guid.NewGuid(), "SHIFT_REMOVED", "Shift", shift.Id.ToString());
                return Task.FromResult<ShiftChangeDecision>(new ShiftRemoval());
            }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.Removed, result.Outcome);
        Assert.IsNull(await StoredAsync(host, shift.Id));
        Assert.AreEqual("SHIFT_REMOVED", (await AuditActionsAsync(host)).Single());
    }

    [TestMethod]
    public async Task AnOutOfDateVersionChangesNothingAndAsksForNoDecision()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);
        var decided = false;

        var result = await WithScopeAsync(host, (store, _, _) => store.ChangeUnderLockAsync(
            shift.Id, "00000000000007D0", ct =>
            {
                decided = true;
                return Task.FromResult<ShiftChangeDecision>(new ShiftRemoval());
            }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.VersionStale, result.Outcome);
        Assert.AreEqual(Convert.ToHexString(shift.Version), result.Shift?.Version);
        Assert.IsFalse(decided);
        Assert.IsNotNull(await StoredAsync(host, shift.Id));
    }

    [TestMethod]
    public async Task ARefusalChangesNothingAndDiscardsTheStagedAuditEntry()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        var result = await WithScopeAsync(host, (store, audit, _) => store.ChangeUnderLockAsync(
            shift.Id, Convert.ToHexString(shift.Version), ct =>
            {
                audit.Record(Guid.NewGuid(), "SHIFT_CHANGED", "Shift", shift.Id.ToString());
                return Task.FromResult<ShiftChangeDecision>(new ShiftRefusal("CAPACITY_BELOW_HELD"));
            }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.Refused, result.Outcome);
        Assert.AreEqual("CAPACITY_BELOW_HELD", result.RefusalCode);
        Assert.AreEqual(shift.RequiredCount, (await StoredAsync(host, shift.Id))?.RequiredCount);
        Assert.IsEmpty(await AuditActionsAsync(host));
    }

    [TestMethod]
    public async Task AnUnknownShiftIsNotFound()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);

        var result = await WithScopeAsync(host, (store, _, _) => store.ChangeUnderLockAsync(
            Guid.NewGuid(), "00000000000007D0",
            ct => Task.FromResult<ShiftChangeDecision>(new ShiftRemoval()), CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.NotFound, result.Outcome);
    }

    [TestMethod]
    public async Task AStoreFailureDuringTheDecisionIsNotRecordedAndChangesNothing()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        var result = await WithScopeAsync(host, (store, audit, _) => store.ChangeUnderLockAsync(
            shift.Id, Convert.ToHexString(shift.Version), ct =>
            {
                audit.Record(Guid.NewGuid(), "SHIFT_REMOVED", "Shift", shift.Id.ToString());
                throw new TimeoutException("Simulated timeout.");
            }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.NotRecorded, result.Outcome);
        Assert.IsNotNull(await StoredAsync(host, shift.Id));
        Assert.IsEmpty(await AuditActionsAsync(host));
    }

    [TestMethod]
    public async Task TheShiftRowStaysLockedWhileTheDecisionRuns()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);

        var result = await WithScopeAsync(host, (store, _, _) => store.ChangeUnderLockAsync(
            shift.Id, Convert.ToHexString(shift.Version), async ct =>
            {
                var blocked = await Assert.ThrowsExactlyAsync<SqlException>(() => ChangeCapacityAsync(host, shift.Id, 9));
                Assert.AreEqual(1222, blocked.Number, "A concurrent write must wait for the shift lock.");
                return new ShiftUpdate(shift.LocationId, shift.StartsAt, shift.EndsAt, 4);
            }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.Saved, result.Outcome);
        Assert.AreEqual(4, (await StoredAsync(host, shift.Id))?.RequiredCount);
    }

    [TestMethod]
    public async Task TheDecisionReadsVolunteerAssignmentsInsideTheTransaction()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = await SeedShiftAsync(host);
        await host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                VolunteerId = Guid.NewGuid(),
                Status = AssignmentStatus.Active,
                AssignedAt = Now,
            });
            return db.SaveChangesAsync();
        });
        var held = -1;

        var result = await WithScopeAsync(host, (store, _, assignments) => store.ChangeUnderLockAsync(
            shift.Id, Convert.ToHexString(shift.Version), async ct =>
            {
                held = await assignments.CountHeldAsync(shift.Id, ct);
                return new ShiftRefusal("ASSIGNED_SHIFT_CHANGE_UNRESOLVED");
            }, CancellationToken.None));

        Assert.AreEqual(ShiftChangeOutcome.Refused, result.Outcome);
        Assert.AreEqual(1, held);
    }

    private static async Task<Shift> SeedShiftAsync(SqlTestHost host)
    {
        var shift = ShiftServiceTests.Shift(Guid.NewGuid(), Now.AddDays(60));
        await host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(shift);
            return db.SaveChangesAsync();
        });
        return shift;
    }

    private static Task<Shift?> StoredAsync(SqlTestHost host, Guid shiftId) => host.WithAsync<AdmDbContext, Shift?>(db =>
        db.Shifts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shiftId));

    private static Task<List<string>> AuditActionsAsync(SqlTestHost host) => host.WithAsync<AdmDbContext, List<string>>(db =>
        db.AuditEntries.AsNoTracking().Select(x => x.Action).ToListAsync());

    private static async Task<T> WithScopeAsync<T>(
        SqlTestHost host, Func<IShiftStore, IAdminAuditLog, IAssignmentQueryService, Task<T>> action)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await action(
            scope.ServiceProvider.GetRequiredService<IShiftStore>(),
            scope.ServiceProvider.GetRequiredService<IAdminAuditLog>(),
            scope.ServiceProvider.GetRequiredService<IAssignmentQueryService>());
    }

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
}
