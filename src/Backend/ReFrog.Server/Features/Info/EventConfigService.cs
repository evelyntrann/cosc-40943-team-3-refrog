namespace ReFrog.Server.Features.Info;

/// <summary>An event as other areas see it. Dates are calendar dates in America/Chicago.</summary>
public sealed record ConfiguredEvent(Guid Id, int EventYear, DateOnly StartDate, DateOnly EndDate, string Status);

/// <summary>One weekday's operating hours at a location, in America/Chicago local time.</summary>
public sealed record OperatingHours(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

/// <summary>A location of an event with its weekly operating hours (<c>BR-event-schedule</c>).</summary>
public sealed record ConfiguredLocation(Guid Id, string Name, string Status, IReadOnlyList<OperatingHours> Hours);

/// <summary>INF's read access to event configuration (architecture §8.2.8: business code depends on the store interface).</summary>
public interface IEventConfigStore
{
    /// <summary>The one event whose status is PLANNED or ACTIVE, or null.</summary>
    Task<ConfiguredEvent?> FindCurrentEventAsync(CancellationToken cancellationToken);

    Task<ConfiguredEvent?> FindEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfiguredLocation>> ListLocationsAsync(Guid eventId, CancellationToken cancellationToken);
}

/// <summary>
/// The service other areas use to read INF's events, locations, and operating hours (docs/design/INF.md;
/// ADM.md key decision 11). It has no write methods until a use case configures events (<c>OI-31</c>).
/// </summary>
public sealed class EventConfigService(IEventConfigStore store)
{
    /// <summary>The current event: the one event whose status is PLANNED or ACTIVE, or null when there is none.</summary>
    public Task<ConfiguredEvent?> GetCurrentEventAsync(CancellationToken cancellationToken) =>
        store.FindCurrentEventAsync(cancellationToken);

    /// <summary>Any event by id, or null when it does not exist.</summary>
    public Task<ConfiguredEvent?> FindEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        store.FindEventAsync(eventId, cancellationToken);

    /// <summary>The event's locations with their weekly operating hours, ordered by name.</summary>
    public Task<IReadOnlyList<ConfiguredLocation>> GetLocationsForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        store.ListLocationsAsync(eventId, cancellationToken);
}
