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

/// <summary>What a write did. <see cref="NotRecorded"/> means nothing was stored (§8.2.1).</summary>
public enum SaveOutcome
{
    Saved,
    DuplicateRequest,
    NotRecorded,
}

/// <summary>ADM's persistence abstraction for shifts and location defaults (architecture §8.2.8; ADM.md data model).</summary>
public interface IShiftStore
{
    Task<StoredShift?> FindByIdAsync(Guid shiftId, CancellationToken cancellationToken);

    Task<StoredShift?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>The event's shifts that start after <paramref name="now"/>, earliest first.</summary>
    Task<IReadOnlyList<StoredShift>> ListStartingAfterAsync(Guid eventId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the shift under an update lock held until the scope's open shared transaction ends
    /// (<c>SqlUnitOfWork</c>). Throws <see cref="InvalidOperationException"/> when no transaction is open.
    /// </summary>
    Task<StoredShift?> LockAsync(Guid shiftId, CancellationToken cancellationToken);

    /// <summary>Each location's default required count for the event, by location id.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetLocationDefaultsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the shift together with any audit entry already staged by <see cref="IAdminAuditLog"/>, in one
    /// transaction. <see cref="SaveOutcome.DuplicateRequest"/> when its request id is already stored.
    /// </summary>
    Task<SaveOutcome> AddShiftAsync(StoredShift shift, Guid actorId, DateTimeOffset createdAt, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces a location's default, with any staged audit entry, in one transaction.</summary>
    Task<SaveOutcome> SetLocationDefaultAsync(Guid eventId, Guid locationId, int count, CancellationToken cancellationToken);
}
