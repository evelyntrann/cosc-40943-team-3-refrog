namespace ReFrog.Server.Features.Administration;

/// <summary>A stored shift as ADM's business code sees it. Start and end are UTC instants.</summary>
public sealed record StoredShift(
    Guid Id,
    Guid EventId,
    Guid LocationId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int RequiredCount,
    Guid CreateRequestId);

/// <summary>ADM's persistence abstraction for shifts (architecture §8.2.8; ADM.md data model).</summary>
public interface IShiftStore
{
    Task<StoredShift?> FindByIdAsync(Guid shiftId, CancellationToken cancellationToken);

    /// <summary>The event's shifts that start after <paramref name="now"/>, earliest first.</summary>
    Task<IReadOnlyList<StoredShift>> ListStartingAfterAsync(Guid eventId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the shift under an update lock held until the scope's open shared transaction ends
    /// (<c>SqlUnitOfWork</c>). Throws <see cref="InvalidOperationException"/> when no transaction is open.
    /// </summary>
    Task<StoredShift?> LockAsync(Guid shiftId, CancellationToken cancellationToken);
}
