using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Persistence;

namespace ReFrog.Server.Features.Administration.Persistence;

public sealed class EfShiftStore(AdmDbContext db, SqlUnitOfWork unitOfWork, ILogger<EfShiftStore> logger) : IShiftStore
{
    // The failure type only, like SessionService: no exception text, which can quote stored values (§8.2.7).
    private static readonly Action<ILogger, string, Exception?> LogNotRecorded =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7202, "AdministrationWriteNotRecorded"),
            "An administration change could not be stored ({FailureType})");

    public async Task<StoredShift?> FindByIdAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shiftId, cancellationToken);
        return shift is null ? null : ToStored(shift);
    }

    public async Task<StoredShift?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(x => x.CreateRequestId == requestId, cancellationToken);
        return shift is null ? null : ToStored(shift);
    }

    public async Task<IReadOnlyList<StoredShift>> ListStartingAfterAsync(
        Guid eventId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var shifts = await db.Shifts.AsNoTracking()
            .Where(x => x.EventId == eventId && x.StartsAt > now)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return shifts.Select(ToStored).ToList();
    }

    public async Task<IReadOnlyList<StoredShift>> ListForEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var shifts = await db.Shifts.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return shifts.Select(ToStored).ToList();
    }

    public async Task<StoredShift?> LockAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        // Joins the caller's transaction on the shared connection, so the lock lasts until that caller commits
        // (ADM.md, ShiftService; VOL.md "How overbooking is prevented").
        await unitOfWork.EnlistAsync(db, cancellationToken);
        var shift = await db.Shifts
            .FromSqlInterpolated($"SELECT * FROM [adm].[Shifts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {shiftId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return shift is null ? null : ToStored(shift);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetLocationDefaultsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.LocationStaffing.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .ToDictionaryAsync(x => x.LocationId, x => x.DefaultRequiredCount, cancellationToken);
    }

    public async Task<ShiftSaveResult> AddShiftAsync(
        StoredShift shift, Guid actorId, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shift);
        StoredShift? existing = null;
        var outcome = await InTransactionAsync(async ct =>
        {
            // Holds the request id's key range until commit, so a concurrent retry waits and then finds this shift
            // instead of failing on the unique index (ROB-duplicate-submission).
            var stored = await db.Shifts
                .FromSqlInterpolated(
                    $"SELECT * FROM [adm].[Shifts] WITH (UPDLOCK, HOLDLOCK) WHERE [CreateRequestId] = {shift.CreateRequestId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(ct);
            if (stored is not null)
            {
                existing = ToStored(stored);
                return false;
            }

            db.Shifts.Add(new Shift
            {
                Id = shift.Id,
                EventId = shift.EventId,
                LocationId = shift.LocationId,
                StartsAt = shift.StartsAt,
                EndsAt = shift.EndsAt,
                RequiredCount = shift.RequiredCount,
                CreateRequestId = shift.CreateRequestId,
                CreatedByAccountId = actorId,
                CreatedAt = createdAt,
            });
            return true;
        }, cancellationToken);
        return outcome == SaveOutcome.Saved || existing is null
            ? new ShiftSaveResult(outcome)
            : new ShiftSaveResult(SaveOutcome.DuplicateRequest, existing);
    }

    public Task<SaveOutcome> SetLocationDefaultAsync(
        Guid eventId, Guid locationId, int count, CancellationToken cancellationToken) =>
        InTransactionAsync(async ct =>
        {
            // Locks the (event, location) key, even when no row exists yet, so two first-time saves cannot both insert.
            var row = await db.LocationStaffing
                .FromSqlInterpolated(
                    $"SELECT * FROM [adm].[LocationStaffing] WITH (UPDLOCK, HOLDLOCK) WHERE [EventId] = {eventId} AND [LocationId] = {locationId}")
                .SingleOrDefaultAsync(ct);
            if (row is null)
            {
                db.LocationStaffing.Add(new LocationStaffing
                {
                    EventId = eventId,
                    LocationId = locationId,
                    DefaultRequiredCount = count,
                });
            }
            else
            {
                row.DefaultRequiredCount = count;
            }

            return true;
        }, cancellationToken);

    public async Task<ShiftChangeResult> ChangeUnderLockAsync(
        Guid shiftId,
        string expectedVersion,
        Func<CancellationToken, Task<ShiftChangeDecision>> decide,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decide);
        Shift? row = null;
        ShiftChangeResult? unchanged = null;
        var removed = false;
        var outcome = await InTransactionAsync(async ct =>
        {
            // The same row lock as LockAsync, so a sign-up or cancellation on this shift waits for this change.
            row = await db.Shifts
                .FromSqlInterpolated($"SELECT * FROM [adm].[Shifts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {shiftId}")
                .SingleOrDefaultAsync(ct);
            if (row is null)
            {
                unchanged = new ShiftChangeResult(ShiftChangeOutcome.NotFound);
                return false;
            }

            var current = ToStored(row);
            if (!string.Equals(current.Version, expectedVersion, StringComparison.OrdinalIgnoreCase))
            {
                unchanged = new ShiftChangeResult(ShiftChangeOutcome.VersionStale, current);
                return false;
            }

            switch (await decide(ct))
            {
                case ShiftUpdate update:
                    row.LocationId = update.LocationId;
                    row.StartsAt = update.StartsAt;
                    row.EndsAt = update.EndsAt;
                    row.RequiredCount = update.RequiredCount;
                    return true;
                case ShiftRemoval:
                    db.Shifts.Remove(row);
                    removed = true;
                    return true;
                case ShiftRefusal refusal:
                    unchanged = new ShiftChangeResult(ShiftChangeOutcome.Refused, current, refusal.Code);
                    return false;
                default:
                    throw new InvalidOperationException("Unknown shift change decision.");
            }
        }, cancellationToken);

        if (outcome == SaveOutcome.NotRecorded)
        {
            return new ShiftChangeResult(ShiftChangeOutcome.NotRecorded);
        }

        if (unchanged is not null)
        {
            return unchanged;
        }

        return removed
            ? new ShiftChangeResult(ShiftChangeOutcome.Removed)
            : new ShiftChangeResult(ShiftChangeOutcome.Saved, ToStored(row!));
    }

    /// <summary>
    /// One shared transaction for the change and its staged audit entry (§8.2.8, §8.2.9). <paramref name="prepare"/>
    /// runs inside it and returns false to store nothing. Every path that does not commit discards what the
    /// context tracks, including the staged audit entry.
    /// </summary>
    private async Task<SaveOutcome> InTransactionAsync(Func<CancellationToken, Task<bool>> prepare, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
            await unitOfWork.EnlistAsync(db, cancellationToken);
            if (!await prepare(cancellationToken))
            {
                return SaveOutcome.DuplicateRequest;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            LogNotRecorded(logger, exception.GetType().Name, null);
            return SaveOutcome.NotRecorded;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// A failure of the store, as opposed to a bug. EF Core's SQL Server strategy wraps a transient error (deadlock,
    /// dropped connection, timeout) in <see cref="InvalidOperationException"/>; RegistrationService checks the same.
    /// </summary>
    private static bool IsStoreFailure(Exception failure) =>
        failure is DbUpdateException or DbException or TimeoutException ||
        (failure is InvalidOperationException &&
         failure.InnerException is DbUpdateException or DbException or TimeoutException);

    internal static StoredShift ToStored(Shift shift) => new(shift.Id, shift.EventId, shift.LocationId,
        shift.StartsAt.ToUniversalTime(), shift.EndsAt.ToUniversalTime(), shift.RequiredCount, shift.CreateRequestId,
        Convert.ToHexString(shift.Version));
}
