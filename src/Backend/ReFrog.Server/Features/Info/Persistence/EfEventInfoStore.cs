using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Info.Persistence;

public sealed class EfEventInfoStore(InfoDbContext db) : IEventInfoStore
{
    public Task<EventInfo?> FindActiveEventAsync(CancellationToken cancellationToken) =>
        db.Events.AsNoTracking()
            .Where(x => x.Status == ReFrogEventStatus.Active)
            .Select(x => new EventInfo(x.EventYear, x.StartDate, x.EndDate, x.DailyOperatingHoursDescription,
                x.CampusOverview, x.OverallInfographicUrl))
            .SingleOrDefaultAsync(cancellationToken);
}
