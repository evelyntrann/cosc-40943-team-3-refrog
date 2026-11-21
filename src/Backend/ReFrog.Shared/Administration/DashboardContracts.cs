namespace ReFrog.Contracts.Administration;

/// <summary>
/// <c>UC-ADM-view-dashboard</c>: volunteer coverage for every location of the current event, including locations
/// with no activity (3a). Donation and shopping counts are added once <c>UC-DON-log-donation</c> and
/// <c>UC-SHP-log-item-taken</c> record them (issue #136).
/// </summary>
public sealed record DashboardResponse(IReadOnlyList<DashboardLocation> Locations);

/// <summary>A location's coverage: its shifts summed, and each shift in start order.</summary>
public sealed record DashboardLocation(
    Guid LocationId,
    string Name,
    string Status,
    bool HasActivity,
    bool HasUnfilledShifts,
    DashboardCoverage Coverage,
    IReadOnlyList<DashboardShift> Shifts);

public sealed record DashboardShift(
    Guid ShiftId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool Unfilled,
    DashboardCoverage Coverage);

/// <summary>
/// Held is <c>ACTIVE</c> plus <c>PENDING</c>; open is the slots no one holds (ADM.md sequence, step 4).
/// </summary>
public sealed record DashboardCoverage(int RequiredCount, int ActiveCount, int PendingCount, int HeldCount, int OpenCount);
