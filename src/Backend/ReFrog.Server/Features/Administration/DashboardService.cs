using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Volunteer;

namespace ReFrog.Server.Features.Administration;

/// <summary>
/// <c>UC-ADM-view-dashboard</c> steps 3–4 and 3a: volunteer coverage by location, computed from the stored shifts and
/// VOL's assignments rather than a separate counter (ADM.md key decision 1). The caller has already passed
/// <see cref="AdminAuthorization"/> (2a).
/// </summary>
public sealed class DashboardService(
    EventConfigService events,
    IShiftStore shifts,
    IAssignmentQueryService assignments,
    ILogger<DashboardService> logger)
{
    private static readonly AssignmentCounts NoneHeld = new(0, 0);

    private static readonly Action<ILogger, string, Exception?> LogDashboardUnavailable =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7206, "DashboardUnavailable"),
            "Dashboard information could not be read ({FailureType})");

    /// <summary>
    /// Every location of the current event, in INF's order. If any source cannot answer, the whole answer is
    /// unavailable, never a partial or zero total (<c>FR-DISPLAY-unavailable-data</c>).
    /// </summary>
    public async Task<AdminReply<DashboardResponse>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var current = await events.GetCurrentEventAsync(cancellationToken);
            if (current is null)
            {
                return Failure(409, ApiErrorCategory.Validation, "NO_CURRENT_EVENT",
                    "There is no planned or active event to show.");
            }

            var locations = await events.GetLocationsForEventAsync(current.Id, cancellationToken);
            var stored = await shifts.ListForEventAsync(current.Id, cancellationToken);
            var counts = await assignments.CountsByShiftsAsync(stored.Select(x => x.Id).ToList(), cancellationToken);
            var byLocation = stored.ToLookup(x => x.LocationId);
            return new(200, ApiResponse.Success(new DashboardResponse(locations
                .Select(location => Summarize(location, byLocation[location.Id], counts))
                .ToList())));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogDashboardUnavailable(logger, exception.GetType().Name, null);
            return Failure(503, ApiErrorCategory.Unavailable, "UNAVAILABLE",
                "Dashboard information is temporarily unavailable. Try again.");
        }
    }

    /// <summary>
    /// A location with no held sign-ups has no activity yet but stays listed (3a). Open slots are summed per shift,
    /// so one shift's spare capacity never hides another's gap.
    /// </summary>
    private static DashboardLocation Summarize(
        ConfiguredLocation location, IEnumerable<StoredShift> locationShifts, IReadOnlyDictionary<Guid, AssignmentCounts> counts)
    {
        var shiftSummaries = locationShifts
            .Select(shift => ShiftCoverage(shift, counts.GetValueOrDefault(shift.Id, NoneHeld)))
            .ToList();
        var coverage = new DashboardCoverage(
            shiftSummaries.Sum(x => x.Coverage.RequiredCount),
            shiftSummaries.Sum(x => x.Coverage.ActiveCount),
            shiftSummaries.Sum(x => x.Coverage.PendingCount),
            shiftSummaries.Sum(x => x.Coverage.HeldCount),
            shiftSummaries.Sum(x => x.Coverage.OpenCount));
        return new DashboardLocation(
            location.Id,
            location.Name,
            location.Status,
            coverage.HeldCount > 0,
            shiftSummaries.Any(x => x.Unfilled),
            coverage,
            shiftSummaries);
    }

    private static DashboardShift ShiftCoverage(StoredShift shift, AssignmentCounts held)
    {
        var open = Math.Max(shift.RequiredCount - held.Held, 0);
        return new DashboardShift(shift.Id, shift.StartsAt, shift.EndsAt, open > 0,
            new DashboardCoverage(shift.RequiredCount, held.Active, held.Pending, held.Held, open));
    }

    private static AdminReply<DashboardResponse> Failure(int status, ApiErrorCategory category, string code, string message) =>
        new(status, ApiResponse.Failure<DashboardResponse>(new ApiError(category, code, message)));
}
