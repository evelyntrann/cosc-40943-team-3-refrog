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
}
