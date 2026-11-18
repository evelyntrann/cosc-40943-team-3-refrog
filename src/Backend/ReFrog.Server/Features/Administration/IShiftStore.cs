namespace ReFrog.Server.Features.Administration;

/// <summary>
/// A stored shift as ADM's business code sees it. Start and end are UTC instants. <see cref="Version"/> is the row's
/// version as hexadecimal text; it is empty on a shift that has not been stored yet.
/// </summary>
public sealed record StoredShift(
    Guid Id,
    Guid EventId,
    Guid LocationId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int RequiredCount,
    Guid CreateRequestId,
    string Version = "");

/// <summary>What to do with a shift once its row is locked (<see cref="IShiftStore.ChangeUnderLockAsync"/>).</summary>
public abstract record ShiftChangeDecision;

/// <summary>Store these details on the shift.</summary>
public sealed record ShiftUpdate(Guid LocationId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int RequiredCount)
    : ShiftChangeDecision;

/// <summary>Delete the shift.</summary>
public sealed record ShiftRemoval : ShiftChangeDecision;

/// <summary>Change nothing, for the reason <paramref name="Code"/> names.</summary>
public sealed record ShiftRefusal(string Code) : ShiftChangeDecision;

/// <summary>What a locked change did. Every outcome except <see cref="Saved"/> and <see cref="Removed"/> changed nothing.</summary>
public enum ShiftChangeOutcome
{
    Saved,
    Removed,
    NotFound,
    VersionStale,
    Refused,
    NotRecorded,
}

/// <summary>
/// The result of a locked change. <see cref="Shift"/> is the shift as saved, or as found when the version was stale
/// or the change refused. <see cref="RefusalCode"/> is set on <see cref="ShiftChangeOutcome.Refused"/>.
/// </summary>
public sealed record ShiftChangeResult(ShiftChangeOutcome Outcome, StoredShift? Shift = null, string? RefusalCode = null);

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

    /// <summary>All of the event's shifts, earliest first.</summary>
    Task<IReadOnlyList<StoredShift>> ListForEventAsync(Guid eventId, CancellationToken cancellationToken);

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

    /// <summary>
    /// Changes one shift in one transaction, holding the row lock VOL takes for sign-ups and cancellations
    /// (ADM.md key decision 8). When the row's version is <paramref name="expectedVersion"/>, <paramref name="decide"/>
    /// runs under that lock, so the held count it reads cannot change before the decision is stored. An audit entry
    /// it stages commits with the change and is discarded on every other outcome.
    /// </summary>
    Task<ShiftChangeResult> ChangeUnderLockAsync(
        Guid shiftId,
        string expectedVersion,
        Func<StoredShift, CancellationToken, Task<ShiftChangeDecision>> decide,
        CancellationToken cancellationToken);
}
