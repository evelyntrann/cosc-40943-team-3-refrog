using ReFrog.Contracts.Info;
using ReFrog.Server.Features.Info;

namespace ReFrog.Server.Tests.Info;

// UC-INF-view-event-info (docs/design/INF.md test rows "main" and "2a").
[TestClass]
public sealed class InfoServiceTests
{
    [TestMethod]
    public async Task NoActiveEventReturnsOffSeasonWithoutEventFields()
    {
        var service = new InfoService(new FixedStore(null));

        var info = await service.GetEventInfoAsync(CancellationToken.None);

        Assert.AreEqual(EventInfoStatus.OffSeason, info.Status);
        Assert.IsNull(info.EventYear);
        Assert.IsNull(info.ScheduleWindow);
        Assert.IsNull(info.CampusOverview);
        Assert.IsNull(info.OverallInfographicUrl);
    }

    [TestMethod]
    public async Task ActiveEventReturnsItsDatesHoursAndOverview()
    {
        var stored = new EventInfo(2027, new DateOnly(2027, 5, 3), new DateOnly(2027, 5, 8),
            "Sample hours text", "Sample overview text", null);
        var service = new InfoService(new FixedStore(stored));

        var info = await service.GetEventInfoAsync(CancellationToken.None);

        Assert.AreEqual(EventInfoStatus.Active, info.Status);
        Assert.AreEqual(2027, info.EventYear);
        Assert.AreEqual(new EventScheduleWindow(stored.StartDate, stored.EndDate, "Sample hours text"), info.ScheduleWindow);
        Assert.AreEqual("Sample overview text", info.CampusOverview);
        Assert.IsNull(info.OverallInfographicUrl);
    }

    private sealed class FixedStore(EventInfo? active) : IEventInfoStore
    {
        public Task<EventInfo?> FindActiveEventAsync(CancellationToken cancellationToken) => Task.FromResult(active);
    }
}
