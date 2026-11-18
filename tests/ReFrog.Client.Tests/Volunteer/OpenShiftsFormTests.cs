using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Volunteer;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;

namespace ReFrog.Client.Tests.Volunteer;

// The volunteer open shifts form for UC-VOL-signup:
// step 2 open list, 2a empty list, step 6 active confirmation, 5c2 pending approval, 4b/5b already assigned,
// and 4a/4c/1a error feedback.
[TestClass]
public sealed class OpenShiftsFormTests
{
    private static readonly Guid ShiftId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    [TestMethod]
    public async Task LoadPopulatesOpenShiftsList()
    {
        var server = new ScriptedServer();
        var form = server.Form();

        await form.LoadAsync();

        Assert.IsNull(form.LoadError);
        Assert.AreEqual(1, form.Shifts.Count);
        Assert.AreEqual("Commons", form.Shifts[0].LocationName);
        Assert.AreEqual(2, form.Shifts[0].OpenSlots);
        Assert.IsFalse(form.Shifts[0].AssignedToMe);
    }

    [TestMethod]
    public async Task LoadSetsLoadErrorOnServerRefusal()
    {
        var server = new ScriptedServer
        {
            ListResponse = (HttpStatusCode.Forbidden, ApiResponse.Failure<IReadOnlyList<OpenShiftDto>>(new ApiError(
                ApiErrorCategory.Authorization, "VOLUNTEER_ACCESS_INACTIVE", "Volunteer access is inactive.")))
        };
        var form = server.Form();

        await form.LoadAsync();

        Assert.AreEqual(0, form.Shifts.Count);
        Assert.IsNotNull(form.LoadError);
        Assert.AreEqual("VOLUNTEER_ACCESS_INACTIVE", form.LoadError.Code);
    }

    [TestMethod]
    public async Task SignUpConfirmedSetsConfirmedFeedbackMessage()
    {
        var server = new ScriptedServer();
        var form = server.Form();

        var result = await form.SignUpAsync(ShiftId);

        Assert.IsNotNull(result);
        Assert.AreEqual(AssignmentStatus.Active, result.Status);
        Assert.AreEqual("Sign-up confirmed!", form.FeedbackMessage);
        Assert.IsNull(form.SignUpError);
    }

    [TestMethod]
    public async Task SignUpPendingSetsAwaitingApprovalFeedbackMessage()
    {
        var server = new ScriptedServer
        {
            SignUpResponse = (HttpStatusCode.Created, ApiResponse.Success(new SignUpResultDto(
                Guid.NewGuid(), ShiftId, AssignmentStatus.Pending, 1, false)))
        };
        var form = server.Form();

        var result = await form.SignUpAsync(ShiftId);

        Assert.IsNotNull(result);
        Assert.AreEqual(AssignmentStatus.Pending, result.Status);
        Assert.AreEqual("Signed up, awaiting administrator approval.", form.FeedbackMessage);
    }

    [TestMethod]
    public async Task SignUpAlreadyAssignedSetsAlreadyAssignedFeedbackMessage()
    {
        var server = new ScriptedServer
        {
            SignUpResponse = (HttpStatusCode.OK, ApiResponse.Success(new SignUpResultDto(
                Guid.NewGuid(), ShiftId, AssignmentStatus.Active, 1, true)))
        };
        var form = server.Form();

        var result = await form.SignUpAsync(ShiftId);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.AlreadyAssigned);
        Assert.AreEqual("You are already signed up for this shift.", form.FeedbackMessage);
    }

    [TestMethod]
    public async Task SignUpRefusalSetsSignUpError()
    {
        var server = new ScriptedServer
        {
            SignUpResponse = (HttpStatusCode.Conflict, ApiResponse.Failure<SignUpResultDto>(new ApiError(
                ApiErrorCategory.Conflict, "SHIFT_FULL", "This shift is already full.")))
        };
        var form = server.Form();

        var result = await form.SignUpAsync(ShiftId);

        Assert.IsNull(result);
        Assert.IsNotNull(form.SignUpError);
        Assert.AreEqual("SHIFT_FULL", form.SignUpError.Code);
        Assert.AreEqual("This shift is already full.", form.SignUpError.Message);
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        public (HttpStatusCode, ApiResponse<IReadOnlyList<OpenShiftDto>>) ListResponse { get; set; } =
            (HttpStatusCode.OK, ApiResponse.Success<IReadOnlyList<OpenShiftDto>>(new[]
            {
                new OpenShiftDto(ShiftId, EventId, LocationId, "Commons",
                    new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2027, 5, 3, 23, 0, 0, TimeSpan.Zero),
                    3, 2, false)
            }));

        public (HttpStatusCode, ApiResponse<SignUpResultDto>) SignUpResponse { get; set; } =
            (HttpStatusCode.Created, ApiResponse.Success(new SignUpResultDto(
                Guid.NewGuid(), ShiftId, AssignmentStatus.Active, 1, false)));

        public OpenShiftsForm Form() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/shifts/open")
            {
                return Task.FromResult(Json(ListResponse.Item1, ListResponse.Item2));
            }

            if (path.StartsWith("/api/v1/shifts/") && path.EndsWith("/assignments"))
            {
                return Task.FromResult(Json(SignUpResponse.Item1, SignUpResponse.Item2));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, ApiResponse<T> body) =>
            new(status) { Content = JsonContent.Create(body) };
    }
}
