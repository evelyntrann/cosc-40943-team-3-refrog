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

/// <summary>
/// The result of storing a shift. On <see cref="SaveOutcome.DuplicateRequest"/>, <see cref="Existing"/> is the shift
/// already stored for that request id.
/// </summary>
public sealed record ShiftSaveResult(SaveOutcome Outcome, StoredShift? Existing = null);

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
    /// transaction. If its request id is already stored (a concurrent retry), stores nothing and returns
    /// <see cref="SaveOutcome.DuplicateRequest"/> with that shift. Any other failure stores nothing and discards the
    /// staged audit entry.
    /// </summary>
    Task<ShiftSaveResult> AddShiftAsync(StoredShift shift, Guid actorId, DateTimeOffset createdAt, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or replaces a location's default, with any staged audit entry, in one transaction; concurrent saves
    /// for the same location run one after the other. A failure stores nothing and discards the staged entry.
    /// </summary>
    Task<SaveOutcome> SetLocationDefaultAsync(Guid eventId, Guid locationId, int count, CancellationToken cancellationToken);
}
