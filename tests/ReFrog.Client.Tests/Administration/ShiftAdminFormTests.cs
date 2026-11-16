using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Administration;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Tests.Administration;

// The admin shift page for UC-ADM-manage-shifts: step 5 confirmation, 2a1 refusal, 4a1 kept input, and 4b retry
// with the same request id (ADM.md key decision 13; FR-SAVE-confirmed-success).
[TestClass]
public sealed class ShiftAdminFormTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid Commons = Guid.NewGuid();

    [TestMethod]
    public async Task LoadShowsTheEventLocationsAndTheirDefaults()
    {
        var server = new ScriptedServer();
        var form = server.Form();

        await form.LoadAsync();

        Assert.IsNull(form.LoadError);
        Assert.AreEqual("Commons", form.Options?.Locations.Single().Name);
        Assert.AreEqual(3, form.DefaultInputs[Commons]);
    }

    [TestMethod]
    public async Task ARefusedCallerSeesTheServersMessageAndNoOptions()
    {
        var server = new ScriptedServer
        {
            Options = (HttpStatusCode.Forbidden, ApiResponse.Failure<ShiftOptionsResponse>(new ApiError(
                ApiErrorCategory.Authorization, "ADMINISTRATOR_REQUIRED", "Only ReFrog administrators can do this."))),
        };
        var form = server.Form();

        await form.LoadAsync();

        Assert.IsNull(form.Options);
        Assert.AreEqual("ADMINISTRATOR_REQUIRED", form.LoadError?.Code);
    }

    [TestMethod]
    public async Task CreatedShiftIsConfirmedOnlyAfterTheServerAnswersAndTheNextFillGetsANewRequestId()
    {
        var server = new ScriptedServer();
        var form = await FilledFormAsync(server);
        var firstRequestId = form.RequestId;

        var created = await form.SubmitAsync();

        Assert.IsTrue(created);
        Assert.AreEqual(3, form.Created?.RequiredCount);
        Assert.AreEqual("token-1", server.LastToken);
        Assert.AreEqual(new CreateShiftRequest(firstRequestId, Commons, "2027-05-03", "14:00", "18:00", null), server.LastShift);
        Assert.AreNotEqual(firstRequestId, form.RequestId);
        Assert.AreEqual(string.Empty, form.StartTime);
        Assert.AreEqual("2027-05-03", form.Date);
    }

    [TestMethod]
    public async Task RefusedDetailsNameTheFieldAndKeepEverythingEntered()
    {
        var server = new ScriptedServer
        {
            Create = (HttpStatusCode.UnprocessableEntity, ApiResponse.Failure<ShiftResponse>(new ApiError(
                ApiErrorCategory.Validation, "VALIDATION_FAILED", "Check the highlighted fields.",
                new Dictionary<string, string> { ["endTime"] = "Commons closes at 8:00 p.m. on Mondays." }))),
        };
        var form = await FilledFormAsync(server);
        var requestId = form.RequestId;

        Assert.IsFalse(await form.SubmitAsync());

        Assert.AreEqual("Commons closes at 8:00 p.m. on Mondays.", form.FieldError("endTime"));
        Assert.IsNull(form.FieldError("date"));
        Assert.IsNull(form.Created);
        Assert.AreEqual(Commons.ToString(), form.LocationChoice);
        Assert.AreEqual("2027-05-03", form.Date);
        Assert.AreEqual("14:00", form.StartTime);
        Assert.AreEqual("18:00", form.EndTime);
        Assert.AreEqual(requestId, form.RequestId);
    }

    [TestMethod]
    public async Task ADroppedResponseIsReportedAsNotCreatedAndTheRetryReusesTheRequestId()
    {
        var server = new ScriptedServer { DropNextCreate = true };
        var form = await FilledFormAsync(server);
        var requestId = form.RequestId;

        Assert.IsFalse(await form.SubmitAsync());
        Assert.AreEqual("NOT_RECORDED", form.Error?.Code);
        Assert.AreEqual("14:00", form.StartTime);

        Assert.IsTrue(await form.SubmitAsync());
        Assert.AreEqual(requestId, server.LastShift?.RequestId);
    }

    [TestMethod]
    public async Task SavingADefaultUpdatesItAndARefusalIsShownForThatLocation()
    {
        var server = new ScriptedServer();
        var form = server.Form();
        await form.LoadAsync();
        form.DefaultInputs[Commons] = 4;

        Assert.IsTrue(await form.SaveDefaultAsync(Commons));
        Assert.AreEqual(4, form.Options?.Locations.Single().DefaultRequiredCount);
        Assert.AreEqual(new SetLocationStaffingRequest(4), server.LastStaffing);

        server.Staffing = (HttpStatusCode.UnprocessableEntity, ApiResponse.Failure<LocationStaffingResponse>(new ApiError(
            ApiErrorCategory.Validation, "VALIDATION_FAILED", "Check the highlighted fields.",
            new Dictionary<string, string> { ["defaultRequiredCount"] = "Enter a number of volunteers greater than zero." })));
        form.DefaultInputs[Commons] = 0;

        Assert.IsFalse(await form.SaveDefaultAsync(Commons));
        Assert.AreEqual("Enter a number of volunteers greater than zero.", form.DefaultError(Commons));
        Assert.AreEqual(4, form.Options?.Locations.Single().DefaultRequiredCount);
    }

    private static async Task<ShiftAdminForm> FilledFormAsync(ScriptedServer server)
    {
        var form = server.Form();
        await form.LoadAsync();
        form.LocationChoice = Commons.ToString();
        form.Date = "2027-05-03";
        form.StartTime = "14:00";
        form.EndTime = "18:00";
        return form;
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        private int tokens;

        public (HttpStatusCode, ApiResponse<ShiftOptionsResponse>) Options { get; set; } = (HttpStatusCode.OK, ApiResponse.Success(
            new ShiftOptionsResponse(
                new ShiftOptionsEvent(EventId, 2027, new DateOnly(2027, 5, 3), new DateOnly(2027, 5, 8), "PLANNED"),
                [new ShiftOptionsLocation(Commons, "Commons", "OPEN",
                    [new LocationHours(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(20, 0))], 3)])));

        public (HttpStatusCode, ApiResponse<ShiftResponse>) Create { get; set; } = (HttpStatusCode.Created, ApiResponse.Success(
            new ShiftResponse(Guid.NewGuid(), Commons, new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 5, 3, 23, 0, 0, TimeSpan.Zero), 3)));

        public (HttpStatusCode, ApiResponse<LocationStaffingResponse>)? Staffing { get; set; }

        public bool DropNextCreate { get; set; }
        public CreateShiftRequest? LastShift { get; private set; }
        public SetLocationStaffingRequest? LastStaffing { get; private set; }
        public string? LastToken { get; private set; }

        public ShiftAdminForm Form() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/auth/antiforgery")
            {
                return Json(HttpStatusCode.OK, ApiResponse.Success(new AntiforgeryTokenResponse($"token-{++tokens}")));
            }

            if (path == "/api/v1/admin/shift-options")
            {
                return Json(Options.Item1, Options.Item2);
            }

            LastToken = request.Headers.TryGetValues("RequestVerificationToken", out var values) ? values.Single() : null;
            if (path == "/api/v1/admin/shifts")
            {
                LastShift = await request.Content!.ReadFromJsonAsync<CreateShiftRequest>(cancellationToken);
                if (DropNextCreate)
                {
                    DropNextCreate = false;
                    throw new HttpRequestException("Connection dropped.");
                }

                return Json(Create.Item1, Create.Item2);
            }

            LastStaffing = await request.Content!.ReadFromJsonAsync<SetLocationStaffingRequest>(cancellationToken);
            return Staffing is { } refused
                ? Json(refused.Item1, refused.Item2)
                : Json(HttpStatusCode.OK, ApiResponse.Success(new LocationStaffingResponse(EventId, Commons, LastStaffing!.DefaultRequiredCount!.Value)));
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, ApiResponse<T> body) =>
            new(status) { Content = JsonContent.Create(body) };
    }
}
