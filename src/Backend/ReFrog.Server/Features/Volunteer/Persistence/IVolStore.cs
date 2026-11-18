namespace ReFrog.Server.Features.Volunteer.Persistence;

/// <summary>
/// Persistence operations for the <c>vol</c> schema (VOL.md Components, AssignmentRepository).
/// Handles queries, inserts, and reactivations under the caller's shared SqlUnitOfWork transaction.
/// </summary>
public interface IVolStore
{
    Task<int> CountHeldAsync(Guid shiftId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> CountsHeldByShiftsAsync(
        IEnumerable<Guid> shiftIds, CancellationToken cancellationToken);

    Task<ShiftAssignment?> FindByShiftAndVolunteerAsync(
        Guid shiftId, Guid volunteerId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> ListHeldShiftIdsForVolunteerAsync(
        Guid volunteerId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> ListDeniedShiftIdsForVolunteerAsync(
        Guid volunteerId, CancellationToken cancellationToken);

    Task AddAssignmentAsync(ShiftAssignment assignment, CancellationToken cancellationToken);

    Task UpdateAssignmentAsync(ShiftAssignment assignment, CancellationToken cancellationToken);
}
