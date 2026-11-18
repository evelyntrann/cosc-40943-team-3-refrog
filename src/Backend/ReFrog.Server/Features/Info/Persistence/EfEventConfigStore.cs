using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Info.Persistence;

public sealed class EfEventConfigStore(InfoDbContext db) : IEventConfigStore
{
    public Task<ConfiguredEvent?> FindCurrentEventAsync(CancellationToken cancellationToken) =>
        db.Events.AsNoTracking()
            .Where(x => x.Status == ReFrogEventStatus.Planned || x.Status == ReFrogEventStatus.Active)
            .Select(x => new ConfiguredEvent(x.Id, x.EventYear, x.StartDate, x.EndDate, x.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ConfiguredEvent?> FindEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.Events.AsNoTracking()
            .Where(x => x.Id == eventId)
            .Select(x => new ConfiguredEvent(x.Id, x.EventYear, x.StartDate, x.EndDate, x.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConfiguredLocation>> ListLocationsAsync(Guid eventId, CancellationToken cancellationToken)
    {
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
}
