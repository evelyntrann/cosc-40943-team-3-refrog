using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Administration;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;

namespace ReFrog.Client.Tests.Administration;

// The admin dashboard page for UC-ADM-view-dashboard: step 4 display, 2a1 refusal, and an unavailable answer shown
// as unavailable rather than as zeros (FR-DISPLAY-unavailable-data). Sample data only.
[TestClass]
public sealed class DashboardViewTests
{
    private static readonly DashboardShift MondayShift = new(
        Guid.NewGuid(),
        new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 5, 3, 23, 0, 0, TimeSpan.Zero),
        true,
        new DashboardCoverage(4, 2, 1, 3, 1));

    private static readonly DashboardResponse Summary = new([
        new DashboardLocation(Guid.NewGuid(), "Commons", "OPEN", true, true, MondayShift.Coverage, [MondayShift]),
        new DashboardLocation(Guid.NewGuid(), "Village", "OPEN", false, false, new DashboardCoverage(0, 0, 0, 0, 0), []),
    ]);

    [TestMethod]
    public async Task LoadShowsEveryLocationTheServerSent()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(Summary));
        var view = server.View();

        await view.LoadAsync();

        Assert.IsNull(view.Error);
        Assert.IsFalse(view.Loading);
        Assert.AreEqual(Summary.Locations.Count, view.Summary?.Locations.Count);
        Assert.IsFalse(view.Summary!.Locations[1].HasActivity, "3a: the location with no activity is still shown.");
    }

    [TestMethod]
    public async Task ARefusedCallerSeesTheServersMessageAndNoData()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.Forbidden, ApiResponse.Failure<DashboardResponse>(new ApiError(
            ApiErrorCategory.Authorization, "ADMINISTRATOR_REQUIRED", "Only ReFrog administrators can do this.")));
        var view = server.View();

        await view.LoadAsync();

        Assert.IsNull(view.Summary);
        Assert.AreEqual("ADMINISTRATOR_REQUIRED", view.Error?.Code);
    }

    [TestMethod]
    public async Task AFailedReloadDropsTheEarlierAnswerAndARetryRecovers()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(Summary));
        server.Drop();
        server.Reply(HttpStatusCode.ServiceUnavailable, ApiResponse.Failure<DashboardResponse>(new ApiError(
            ApiErrorCategory.Unavailable, "UNAVAILABLE", "Dashboard information is temporarily unavailable. Try again.")));
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(Summary));
        var view = server.View();
        await view.LoadAsync();

        await view.LoadAsync();

        Assert.IsNull(view.Summary, "An earlier answer is not shown as current.");
        Assert.AreEqual(ApiErrorCategory.Unavailable, view.Error?.Category);

        await view.LoadAsync();

        Assert.IsNull(view.Summary);
        Assert.AreEqual("UNAVAILABLE", view.Error?.Code);

        await view.LoadAsync();

        Assert.IsNull(view.Error);
        Assert.IsNotNull(view.Summary);
    }

    [TestMethod]
    public async Task AReplyThatIsNotTheEnvelopeIsUnavailable()
    {
        var server = new ScriptedServer();
        server.Raw("text/html", "<html>Sign in to the network</html>");
        var view = server.View();

        await view.LoadAsync();

        Assert.IsNull(view.Summary);
        Assert.AreEqual("UNAVAILABLE", view.Error?.Code);
    }

    [TestMethod]
    public void ShiftsAndCoverageReadInCentralTime()
    {
        Assert.AreEqual("Monday, May 3, 2027, 2:00 p.m. to 6:00 p.m.", DashboardView.When(MondayShift));
        Assert.AreEqual("3 of 4 volunteers signed up (2 approved, 1 awaiting approval), 1 open",
            DashboardView.Describe(MondayShift.Coverage));
        Assert.AreEqual("0 of 1 volunteer signed up (0 approved, 0 awaiting approval), 1 open",
            DashboardView.Describe(new DashboardCoverage(1, 0, 0, 0, 1)));
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> replies = new();

        public DashboardView View() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        public void Reply<T>(HttpStatusCode status, ApiResponse<T> body) =>
            replies.Enqueue(() => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

        public void Drop() => replies.Enqueue(() => throw new HttpRequestException("Connection dropped."));

        public void Raw(string mediaType, string body) => replies.Enqueue(() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("/api/v1/admin/dashboard", request.RequestUri!.AbsolutePath);
            return Task.FromResult(replies.Dequeue()());
        }
    }
}
