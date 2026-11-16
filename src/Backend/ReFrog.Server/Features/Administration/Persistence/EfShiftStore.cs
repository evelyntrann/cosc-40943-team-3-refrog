using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Persistence;

namespace ReFrog.Server.Features.Administration.Persistence;

public sealed class EfShiftStore(AdmDbContext db, SqlUnitOfWork unitOfWork) : IShiftStore
{
    public async Task<StoredShift?> FindByIdAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shiftId, cancellationToken);
        return shift is null ? null : ToStored(shift);
    }

    public async Task<IReadOnlyList<StoredShift>> ListStartingAfterAsync(
        Guid eventId, DateTimeOffset now, CancellationToken cancellationToken)
    {
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

    internal static StoredShift ToStored(Shift shift) => new(shift.Id, shift.EventId, shift.LocationId,
        shift.StartsAt.ToUniversalTime(), shift.EndsAt.ToUniversalTime(), shift.RequiredCount, shift.CreateRequestId);
}
