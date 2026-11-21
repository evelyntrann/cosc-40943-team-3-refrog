using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Persistence;

namespace ReFrog.Server.Features.Info.Persistence;

public sealed class EfEventConfigStore(InfoDbContext db, SqlUnitOfWork unitOfWork) : IEventConfigStore
{
    private static readonly Guid CurrentEventKey = Guid.Empty;

    public async Task<ConfiguredEvent?> FindCurrentEventAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.Events.AsNoTracking()
            .Where(x => x.Status == ReFrogEventStatus.Planned || x.Status == ReFrogEventStatus.Active)
            .Select(x => new ConfiguredEvent(x.Id, x.EventYear, x.StartDate, x.EndDate, x.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ConfiguredEvent?> LockCurrentEventAsync(CancellationToken cancellationToken)
    {
        // The unique computed index maps every PLANNED/ACTIVE event to Guid.Empty. HOLDLOCK makes this a key-range
        // lock even when that key is absent, so an insert or status change cannot create a current event until the
        // caller's shared transaction ends (VOL.md, UC-VOL-request-access event-rollover race).
        await unitOfWork.EnlistAsync(db, cancellationToken);
        var current = await db.Events
            .FromSqlInterpolated($"SELECT * FROM [info].[ReFrogEvents] WITH (UPDLOCK, HOLDLOCK, INDEX([IX_ReFrogEvents_CurrentEvent])) WHERE [CurrentEventKey] = {CurrentEventKey}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return current is null ? null : ToConfigured(current);
    }

    public async Task<ConfiguredEvent?> FindEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        return await db.Events.AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new ConfiguredEvent(x.Id, x.EventYear, x.StartDate, x.EndDate, x.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConfiguredLocation>> ListLocationsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await unitOfWork.JoinIfOpenAsync(db, cancellationToken);
        var locations = await db.Locations.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Include(x => x.Schedules)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return locations
            .Select(x => new ConfiguredLocation(x.Id, x.Name, x.Status, x.Schedules
                .Select(s => new OperatingHours(ParseDay(s.DayOfWeek), s.OpenTime, s.CloseTime))
                .OrderBy(h => h.Day).ThenBy(h => h.Opens)
                .ToList()))
            .ToList();
    }

    private static DayOfWeek ParseDay(string stored) =>
        Enum.Parse<DayOfWeek>(stored.ToLower(CultureInfo.InvariantCulture), ignoreCase: true);

    private static ConfiguredEvent ToConfigured(ReFrogEvent stored) =>
        new(stored.Id, stored.EventYear, stored.StartDate, stored.EndDate, stored.Status);
}
