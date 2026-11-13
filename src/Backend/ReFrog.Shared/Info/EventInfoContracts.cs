namespace ReFrog.Contracts.Info;

/// <summary>Values of <see cref="EventInfoResponse.Status"/>.</summary>
public static class EventInfoStatus
{
    /// <summary>An active event exists; its dates, hours, and overview are present.</summary>
    public const string Active = "ACTIVE";

    /// <summary>No active event (UC-INF-view-event-info extension 2a); every event field is null.</summary>
    public const string OffSeason = "OFF_SEASON";
}

/// <summary>Event dates are calendar dates in the event's America/Chicago time zone (architecture §8.2.2).</summary>
public sealed record EventScheduleWindow(DateOnly StartDate, DateOnly EndDate, string DailyHoursDescription);

/// <summary>Response of <c>GET /api/v1/info/event</c> (docs/design/INF.md, API contract).</summary>
public sealed record EventInfoResponse(
    string Status,
    int? EventYear,
    EventScheduleWindow? ScheduleWindow,
    string? CampusOverview,
    string? OverallInfographicUrl);
