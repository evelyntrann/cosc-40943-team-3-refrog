using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Volunteer.Persistence;

namespace ReFrog.Server.Features.Volunteer;

public sealed class AssignmentQueryService(IVolStore store) : IAssignmentQueryService
{
    public Task<int> CountHeldAsync(Guid shiftId, CancellationToken cancellationToken) =>
        store.CountHeldAsync(shiftId, cancellationToken);

    public async Task<bool> IsActiveAsync(Guid shiftId, Guid volunteerId, CancellationToken cancellationToken)
    {
        var assignment = await store.FindByShiftAndVolunteerAsync(shiftId, volunteerId, cancellationToken);
        return assignment is not null && assignment.Status == AssignmentStatus.Active;
    }

    public Task<IReadOnlyDictionary<Guid, int>> CountsHeldByShiftsAsync(
        IReadOnlyCollection<Guid> shiftIds, CancellationToken cancellationToken) =>
        store.CountsHeldByShiftsAsync(shiftIds, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, AssignmentCounts>> CountsByShiftsAsync(
        IReadOnlyCollection<Guid> shiftIds, CancellationToken cancellationToken) =>
        store.CountsByShiftsAsync(shiftIds, cancellationToken);

    public async Task<IReadOnlyList<HeldAssignment>> ListHeldAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var held = await store.ListHeldByShiftAsync(shiftId, cancellationToken);
        return held.Select(x => new HeldAssignment(x.VolunteerId, x.Status)).ToList();
    }

    public Task<bool> HasAnyAssignmentAsync(Guid shiftId, CancellationToken cancellationToken) =>
        store.HasAnyAssignmentAsync(shiftId, cancellationToken);
}
