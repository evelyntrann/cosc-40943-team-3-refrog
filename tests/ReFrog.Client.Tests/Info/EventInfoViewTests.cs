using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Info;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Info;

namespace ReFrog.Client.Tests.Info;

// UC-INF-view-event-info client behavior behind EventInfoPanel (docs/design/INF.md test rows "main" and "2a",
// plus the unavailable-and-retry path of architecture §8.2.1). Event values are sample data.
[TestClass]
public sealed class EventInfoViewTests
{
    private static readonly EventInfoResponse ActiveEvent = new(EventInfoStatus.Active, 2027,
        new EventScheduleWindow(new DateOnly(2027, 5, 3), new DateOnly(2027, 5, 8), "Sample hours"), "Sample overview", null);

    [TestMethod]
    public async Task ActiveEventShowsItsDatesAsCalendarDates()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(ActiveEvent));
        var view = server.View();

        await view.LoadAsync();

        Assert.IsTrue(view.IsActive);
        Assert.IsFalse(view.Unavailable);
        Assert.AreEqual("Monday, May 3, 2027 to Saturday, May 8, 2027", view.DateRange);
        Assert.AreEqual("Sample overview", view.Info?.CampusOverview);
    }

    [TestMethod]
    public async Task OffSeasonIsNotTreatedAsActive()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(new EventInfoResponse(EventInfoStatus.OffSeason, null, null, null, null)));
        var view = server.View();

        await view.LoadAsync();

        Assert.IsFalse(view.IsActive);
        Assert.IsFalse(view.Loading);
        Assert.IsFalse(view.Unavailable);
        Assert.IsNull(view.DateRange);
    }

    [TestMethod]
    public async Task ServerErrorIsUnavailableAndRetryRecovers()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.InternalServerError, ApiResponse.Failure<EventInfoResponse>(
            new ApiError(ApiErrorCategory.Unexpected, "UNEXPECTED_ERROR", "The request could not be completed.")));
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(ActiveEvent));
        var view = server.View();

        await view.LoadAsync();
        Assert.IsTrue(view.Unavailable);
        Assert.IsNull(view.Info);

        await view.LoadAsync();
        Assert.IsFalse(view.Unavailable);
        Assert.IsTrue(view.IsActive);
    }

    [TestMethod]
    public async Task DroppedConnectionIsUnavailable()
    {
        var server = new ScriptedServer();
        server.Drop();
        var view = server.View();

        await view.LoadAsync();

        Assert.IsTrue(view.Unavailable);
        Assert.IsFalse(view.Loading);
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> replies = new();

        public EventInfoView View() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        public void Reply<T>(HttpStatusCode status, ApiResponse<T> body) =>
            replies.Enqueue(() => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

        public void Drop() => replies.Enqueue(() => throw new HttpRequestException("Connection dropped."));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("/api/v1/info/event", request.RequestUri!.AbsolutePath);
            return Task.FromResult(replies.Dequeue()());
        }
    }
}
