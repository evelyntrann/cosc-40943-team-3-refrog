namespace ReFrog.Server.Features.Info;

/// <summary>The published facts of one event, as INF reads them.</summary>
public sealed record EventInfo(
    int EventYear,
    DateOnly StartDate,
    DateOnly EndDate,
    string DailyOperatingHoursDescription,
    string CampusOverview,
    string? OverallInfographicUrl);

/// <summary>INF's read access to event data (architecture §8.2.8: business code depends on the store interface).</summary>
public interface IEventInfoStore
{
    /// <summary>The event whose status is ACTIVE, or null when there is none.</summary>
    Task<EventInfo?> FindActiveEventAsync(CancellationToken cancellationToken);
}
