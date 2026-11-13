using ReFrog.Contracts.Info;

namespace ReFrog.Server.Features.Info;

/// <summary>Assembles the public event overview for UC-INF-view-event-info.</summary>
public sealed class InfoService(IEventInfoStore events)
{
    private static readonly EventInfoResponse OffSeason = new(EventInfoStatus.OffSeason, null, null, null, null);

    public async Task<EventInfoResponse> GetEventInfoAsync(CancellationToken cancellationToken)
    {
        var active = await events.FindActiveEventAsync(cancellationToken);
        if (active is null)
        {
            return OffSeason;
        }

        return new EventInfoResponse(
            EventInfoStatus.Active,
            active.EventYear,
            new EventScheduleWindow(active.StartDate, active.EndDate, active.DailyOperatingHoursDescription),
            active.CampusOverview,
            active.OverallInfographicUrl);
    }
}
