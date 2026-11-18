namespace ReFrog.Server.Features.Volunteer;

/// <summary>A held (ACTIVE or PENDING) assignment as other areas see it.</summary>
public sealed record HeldAssignment(Guid VolunteerId, string Status);

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

    /// <summary>Held counts by shift id. A shift with nothing held is left out.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountsHeldByShiftsAsync(
        IReadOnlyCollection<Guid> shiftIds, CancellationToken cancellationToken);

    /// <summary>The shift's held assignments, earliest first (<c>UC-ADM-manage-shifts</c> 6a1).</summary>
    Task<IReadOnlyList<HeldAssignment>> ListHeldAsync(Guid shiftId, CancellationToken cancellationToken);

    /// <summary>Whether the shift has an assignment in any status, including CANCELLED and DENIED.</summary>
    Task<bool> HasAnyAssignmentAsync(Guid shiftId, CancellationToken cancellationToken);
}
