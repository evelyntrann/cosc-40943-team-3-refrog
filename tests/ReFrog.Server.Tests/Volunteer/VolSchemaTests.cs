using Microsoft.EntityFrameworkCore;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Volunteer.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Volunteer;

// VOL.md data model and migration: the vol schema's constraints hold even if a caller skips validation.
[TestClass]
public sealed class VolSchemaTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ShiftAssignmentAcceptsValidStatusesAndRejectsInvalidStatus()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shiftId = Guid.NewGuid();

        // Valid statuses succeed
        foreach (var status in new[] { AssignmentStatus.Active, AssignmentStatus.Pending, AssignmentStatus.Cancelled, AssignmentStatus.Denied })
        {
            var volunteerId = Guid.NewGuid();
            await host.WithAsync<VolDbContext>(db =>
            {
                db.ShiftAssignments.Add(Assignment(shiftId, volunteerId, status));
                return db.SaveChangesAsync();
            });
        }

        // Invalid status is rejected by check constraint
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(Assignment(shiftId, Guid.NewGuid(), "INVALID_STATUS"));
            return db.SaveChangesAsync();
        }));
    }

    [TestMethod]
    public async Task ShiftAssignmentEnforcesUniqueVolunteerPerShift()
    {
        await using var host = await SqlTestHost.CreateAsync(Now);
        var shiftId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();

        await host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(Assignment(shiftId, volunteerId, AssignmentStatus.Active));
            return db.SaveChangesAsync();
        });

        // Duplicate (ShiftId, VolunteerId) is rejected by UQ_ShiftAssignment_Shift_Volunteer
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(Assignment(shiftId, volunteerId, AssignmentStatus.Pending));
            return db.SaveChangesAsync();
        }));
    }

    private static ShiftAssignment Assignment(Guid shiftId, Guid volunteerId, string status) => new()
    {
        Id = Guid.NewGuid(),
        ShiftId = shiftId,
        VolunteerId = volunteerId,
        Status = status,
        AssignedAt = Now,
        CancelledAt = status == AssignmentStatus.Cancelled ? Now : null,
    };
}
