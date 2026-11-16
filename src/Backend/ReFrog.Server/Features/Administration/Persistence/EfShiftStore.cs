using System.Data.Common;
using Microsoft.Data.SqlClient;
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

    public Task<SaveOutcome> AddShiftAsync(
        StoredShift shift, Guid actorId, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shift);
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
        return SaveAsync(cancellationToken);
    }

    public async Task<SaveOutcome> SetLocationDefaultAsync(
        Guid eventId, Guid locationId, int count, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await db.LocationStaffing.SingleOrDefaultAsync(
                x => x.EventId == eventId && x.LocationId == locationId, cancellationToken);
            if (existing is null)
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
                existing.DefaultRequiredCount = count;
            }
        }
        catch (DbException exception)
        {
            LogNotRecorded(logger, exception.GetType().Name, null);
            return SaveOutcome.NotRecorded;
        }

        return await SaveAsync(cancellationToken);
    }

    /// <summary>One transaction for the change and its staged audit entry (§8.2.8, §8.2.9).</summary>
    private async Task<SaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
            await unitOfWork.EnlistAsync(db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 } sql &&
                                                  sql.Message.Contains("CreateRequestId", StringComparison.Ordinal))
        {
            return SaveOutcome.DuplicateRequest;
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            LogNotRecorded(logger, exception.GetType().Name, null);
            return SaveOutcome.NotRecorded;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    internal static StoredShift ToStored(Shift shift) => new(shift.Id, shift.EventId, shift.LocationId,
        shift.StartsAt.ToUniversalTime(), shift.EndsAt.ToUniversalTime(), shift.RequiredCount, shift.CreateRequestId);
}
