namespace ReFrog.Server.Features.Volunteer;

/// <summary>
/// Read-only assignment queries exposed by VOL to other areas (e.g. ADM shift edits, dashboards, notification rechecks)
/// so other areas never read the <c>vol</c> schema directly (architecture §5.2; VOL.md Components).
/// </summary>
public interface IAssignmentQueryService
{
    /// <summary>Number of held (ACTIVE + PENDING) assignments on a shift.</summary>
    Task<int> CountHeldAsync(Guid shiftId, CancellationToken cancellationToken);

    /// <summary>Checks whether a volunteer holds an ACTIVE assignment on a shift.</summary>
    Task<bool> IsActiveAsync(Guid shiftId, Guid volunteerId, CancellationToken cancellationToken);
}
