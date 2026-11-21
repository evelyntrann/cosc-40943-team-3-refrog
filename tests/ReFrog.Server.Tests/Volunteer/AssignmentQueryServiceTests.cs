using Microsoft.Extensions.DependencyInjection;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Volunteer;
using ReFrog.Server.Features.Volunteer.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Volunteer;

// VOL's read contract for UC-ADM-manage-shifts 6a (VOL.md Components). Sample data only.
[TestClass]
public sealed class AssignmentQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task HeldCountsIncludeActiveAndPendingOnly()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var mixed = Guid.NewGuid();
        var released = Guid.NewGuid();
        var empty = Guid.NewGuid();
        await SeedAsync(host,
            Assignment(mixed, AssignmentStatus.Active), Assignment(mixed, AssignmentStatus.Pending),
            Assignment(mixed, AssignmentStatus.Cancelled), Assignment(mixed, AssignmentStatus.Denied),
            Assignment(released, AssignmentStatus.Cancelled));

        var counts = await WithQueriesAsync(host, queries =>
            queries.CountsHeldByShiftsAsync([mixed, released, empty], CancellationToken.None));

        Assert.AreEqual(2, counts[mixed]);
        Assert.IsFalse(counts.ContainsKey(released));
        Assert.IsFalse(counts.ContainsKey(empty));
    }

    // UC-ADM-view-dashboard: ADM.md test row "Dashboard status accuracy".
    [TestMethod]
    public async Task CountsKeepActiveAndPendingApartAndLeaveOutReleasedSlots()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var mixed = Guid.NewGuid();
        var pendingOnly = Guid.NewGuid();
        var released = Guid.NewGuid();
        var empty = Guid.NewGuid();
        await SeedAsync(host,
            Assignment(mixed, AssignmentStatus.Active), Assignment(mixed, AssignmentStatus.Active),
            Assignment(mixed, AssignmentStatus.Pending), Assignment(mixed, AssignmentStatus.Cancelled),
            Assignment(mixed, AssignmentStatus.Denied), Assignment(pendingOnly, AssignmentStatus.Pending),
            Assignment(released, AssignmentStatus.Cancelled), Assignment(released, AssignmentStatus.Denied));

        var counts = await WithQueriesAsync(host, queries =>
            queries.CountsByShiftsAsync([mixed, pendingOnly, released, empty], CancellationToken.None));

        Assert.AreEqual(new AssignmentCounts(2, 1), counts[mixed]);
        Assert.AreEqual(3, counts[mixed].Held);
        Assert.AreEqual(new AssignmentCounts(0, 1), counts[pendingOnly]);
        Assert.IsFalse(counts.ContainsKey(released), "Cancelled and denied sign-ups hold no slot.");
        Assert.IsFalse(counts.ContainsKey(empty));
        Assert.IsEmpty(await WithQueriesAsync(host, queries => queries.CountsByShiftsAsync([], CancellationToken.None)));
    }

    [TestMethod]
    public async Task HeldListNamesEachHeldVolunteerAndStatusEarliestFirst()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shift = Guid.NewGuid();
        var first = Assignment(shift, AssignmentStatus.Pending, Now.AddMinutes(-10));
        var second = Assignment(shift, AssignmentStatus.Active, Now.AddMinutes(-5));
        await SeedAsync(host, second, first,
            Assignment(shift, AssignmentStatus.Cancelled), Assignment(shift, AssignmentStatus.Denied),
            Assignment(Guid.NewGuid(), AssignmentStatus.Active));

        var held = await WithQueriesAsync(host, queries => queries.ListHeldAsync(shift, CancellationToken.None));

        CollectionAssert.AreEqual(
            new[] { new HeldAssignment(first.VolunteerId, AssignmentStatus.Pending), new HeldAssignment(second.VolunteerId, AssignmentStatus.Active) },
            held.ToArray());
    }

    [TestMethod]
    public async Task AnyAssignmentCountsCancelledAndDeniedHistory()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var cancelled = Guid.NewGuid();
        var denied = Guid.NewGuid();
        await SeedAsync(host, Assignment(cancelled, AssignmentStatus.Cancelled), Assignment(denied, AssignmentStatus.Denied));

        Assert.IsTrue(await WithQueriesAsync(host, queries => queries.HasAnyAssignmentAsync(cancelled, CancellationToken.None)));
        Assert.IsTrue(await WithQueriesAsync(host, queries => queries.HasAnyAssignmentAsync(denied, CancellationToken.None)));
        Assert.IsFalse(await WithQueriesAsync(host, queries => queries.HasAnyAssignmentAsync(Guid.NewGuid(), CancellationToken.None)));
    }

    private static ShiftAssignment Assignment(Guid shiftId, string status, DateTimeOffset? assignedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        ShiftId = shiftId,
        VolunteerId = Guid.NewGuid(),
        Status = status,
        AssignedAt = assignedAt ?? Now,
    };

    private static Task SeedAsync(SqlTestHost host, params ShiftAssignment[] assignments) => host.WithAsync<VolDbContext>(db =>
    {
        db.ShiftAssignments.AddRange(assignments);
        return db.SaveChangesAsync();
    });

    private static async Task<T> WithQueriesAsync<T>(SqlTestHost host, Func<IAssignmentQueryService, Task<T>> action)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAssignmentQueryService>());
    }
}
