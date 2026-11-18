using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Administration;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Contracts.Volunteer;

namespace ReFrog.Client.Tests.Administration;

// The admin shift page for UC-ADM-manage-shifts 6a: affected volunteers are shown before confirming (6a1), a change
// is confirmed only as reviewed, and "saved" follows the server's answer (FR-SAVE-confirmed-success).
[TestClass]
public sealed class ShiftChangeFormTests
{
    private const string Version = "00000000000007D0";
    private static readonly Guid Commons = Guid.NewGuid();

    // Monday May 3, 2027, 2:00–6:00 p.m. CDT.
    private static readonly ManagedShift Shift = new(Guid.NewGuid(), Commons, "Commons",
        new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 5, 3, 23, 0, 0, TimeSpan.Zero), 3, 1, Version);

    private static readonly ShiftImpactResponse Allowed = new(Shift.ShiftId, Version, 1,
        [new AffectedVolunteer("volunteer@example.org", AssignmentStatus.Active)], true, null);

    [TestMethod]
    public async Task LoadListsTheShifts()
    {
        var server = new ScriptedServer();
        var form = server.Form();

        await form.LoadAsync();

        Assert.IsNull(form.LoadError);
        Assert.AreEqual(Shift, form.Shifts.Single());
    }

    [TestMethod]
    public async Task ARefusedCallerSeesTheServersMessageAndNoShifts()
    {
        var server = new ScriptedServer
        {
            List = (HttpStatusCode.Forbidden, ApiResponse.Failure<ShiftListResponse>(new ApiError(
                ApiErrorCategory.Authorization, "ADMINISTRATOR_REQUIRED", "Only ReFrog administrators can do this."))),
        };
        var form = server.Form();

        await form.LoadAsync();

        Assert.IsEmpty(form.Shifts);
        Assert.AreEqual("ADMINISTRATOR_REQUIRED", form.LoadError?.Code);
    }

    [TestMethod]
    public void EditingStartsFromTheShiftsDetailsInChicagoTime()
    {
        var form = new ScriptedServer().Form();

        form.BeginEdit(Shift);

        Assert.AreEqual(Commons.ToString(), form.LocationChoice);
        Assert.AreEqual("2027-05-03", form.Date);
        Assert.AreEqual("14:00", form.StartTime);
        Assert.AreEqual("18:00", form.EndTime);
        Assert.AreEqual(3, form.RequiredCount);
    }

    [TestMethod]
    public async Task ReviewShowsTheAffectedVolunteersAndWritesNothing()
    {
        var server = new ScriptedServer();
        var form = server.Form();
        form.BeginEdit(Shift);
        form.RequiredCount = 4;

        await form.ReviewAsync();

        Assert.AreEqual("volunteer@example.org", form.Impact?.AffectedVolunteers.Single().Email);
        Assert.IsTrue(form.CanConfirm);
        StringAssert.Contains(server.LastImpactQuery, "action=edit", StringComparison.Ordinal);
        StringAssert.Contains(server.LastImpactQuery, $"expectedVersion={Version}", StringComparison.Ordinal);
        StringAssert.Contains(server.LastImpactQuery, "date=2027-05-03", StringComparison.Ordinal);
        StringAssert.Contains(server.LastImpactQuery, "requiredCount=4", StringComparison.Ordinal);
        Assert.AreEqual(0, server.Writes);
    }

    [TestMethod]
    public async Task AChangeCannotBeConfirmedBeforeItIsReviewed()
    {
        var server = new ScriptedServer();
        var form = server.Form();
        form.BeginEdit(Shift);

        Assert.IsFalse(await form.ConfirmAsync());

        Assert.AreEqual(0, server.Writes);
    }

    [TestMethod]
    public async Task ChangingADetailAfterTheReviewAsksForANewReview()
    {
        var server = new ScriptedServer();
        var form = server.Form();
        form.BeginEdit(Shift);
        await form.ReviewAsync();

        form.Date = "2027-05-05";

        Assert.IsNull(form.Impact);
        Assert.IsFalse(form.CanConfirm);
        Assert.IsFalse(await form.ConfirmAsync());
        Assert.AreEqual(0, server.Writes);
    }

    [TestMethod]
    public async Task ARefusedReviewExplainsWhyAndCannotBeConfirmed()
    {
        var server = new ScriptedServer
        {
            Impact = (HttpStatusCode.OK, ApiResponse.Success(Allowed with
            {
                Permitted = false,
                RefusalCode = "ASSIGNED_SHIFT_CHANGE_UNRESOLVED",
            })),
        };
        var form = server.Form();
        form.BeginEdit(Shift);
        form.Date = "2027-05-05";

        await form.ReviewAsync();

        StringAssert.Contains(form.RefusalMessage, "cannot be moved or removed yet", StringComparison.Ordinal);
        Assert.IsFalse(form.CanConfirm);
        Assert.IsFalse(await form.ConfirmAsync());
        Assert.AreEqual(0, server.Writes);
    }

    [TestMethod]
    public async Task ACountBelowHeldSaysHowManyAreNeeded()
    {
        var server = new ScriptedServer
        {
            Impact = (HttpStatusCode.OK, ApiResponse.Success(Allowed with
            {
                HeldCount = 2,
                Permitted = false,
                RefusalCode = "CAPACITY_BELOW_HELD",
            })),
        };
        var form = server.Form();
        form.BeginEdit(Shift);
        form.RequiredCount = 1;

        await form.ReviewAsync();

        Assert.AreEqual("2 volunteers have already signed up. Enter at least 2.", form.RefusalMessage);
    }

    [TestMethod]
    public async Task HistoryOnARemovalIsExplainedWithoutSayingVolunteersAreSignedUp()
    {
        var server = new ScriptedServer
        {
            Impact = (HttpStatusCode.OK, ApiResponse.Success(Allowed with
            {
                HeldCount = 0,
                AffectedVolunteers = [],
                Permitted = false,
                RefusalCode = "ASSIGNED_SHIFT_CHANGE_UNRESOLVED",
            })),
        };
        var form = server.Form();

        await form.BeginRemovalAsync(Shift);

        Assert.AreEqual("Volunteers have signed up for this shift before, so it cannot be removed. You can still edit it.", form.RefusalMessage);
    }

    [TestMethod]
    public async Task ConfirmingSavesWithTheVersionAndTokenThenReloadsTheList()
    {
        var server = new ScriptedServer();
        var form = server.Form();
        form.BeginEdit(Shift);
        form.RequiredCount = 4;
        await form.ReviewAsync();

        Assert.IsTrue(await form.ConfirmAsync());

        Assert.AreEqual(new ChangeShiftRequest(Version, Commons, "2027-05-03", "14:00", "18:00", 4), server.LastPatch);
        Assert.AreEqual("token-1", server.LastToken);
        Assert.AreEqual("Shift saved.", form.Confirmation);
        Assert.IsNull(form.Selected);
        Assert.AreEqual(1, server.ListReads);
    }

    [TestMethod]
    public async Task RemovalShowsTheImpactFirstAndDeletesWithTheVersion()
    {
        var server = new ScriptedServer();
        var form = server.Form();

        await form.BeginRemovalAsync(Shift);

        StringAssert.Contains(server.LastImpactQuery, "action=remove", StringComparison.Ordinal);
        Assert.AreEqual(0, server.Writes);

        Assert.IsTrue(await form.ConfirmAsync());

        Assert.AreEqual($"?expectedVersion={Version}", server.LastDeleteQuery);
        Assert.AreEqual("Shift removed.", form.Confirmation);
    }

    [TestMethod]
    public async Task ARefusedSaveNamesTheFieldKeepsTheInputAndAsksForANewReview()
    {
        var server = new ScriptedServer
        {
            Patch = (HttpStatusCode.Conflict, ApiResponse.Failure<ManagedShift>(new ApiError(
                ApiErrorCategory.Validation, "CAPACITY_BELOW_HELD", "Check the highlighted fields.",
                new Dictionary<string, string> { ["requiredCount"] = "Enter at least the number of volunteers already signed up." }))),
        };
        var form = server.Form();
        form.BeginEdit(Shift);
        form.RequiredCount = 1;
        await form.ReviewAsync();

        Assert.IsFalse(await form.ConfirmAsync());

        Assert.AreEqual("Enter at least the number of volunteers already signed up.", form.FieldError("requiredCount"));
        Assert.AreEqual(1, form.RequiredCount);
        Assert.AreEqual(Shift, form.Selected);
        Assert.IsNull(form.Impact);
        Assert.IsNull(form.Confirmation);
    }

    [TestMethod]
    public async Task AShiftThatChangedMeanwhileClosesTheEditAndReloadsTheList()
    {
        var server = new ScriptedServer
        {
            Patch = (HttpStatusCode.Conflict, ApiResponse.Failure<ManagedShift>(new ApiError(
                ApiErrorCategory.Validation, "SHIFT_VERSION_STALE", "This shift changed since you opened it."))),
        };
        var form = server.Form();
        form.BeginEdit(Shift);
        await form.ReviewAsync();

        Assert.IsFalse(await form.ConfirmAsync());

        Assert.AreEqual("SHIFT_VERSION_STALE", form.Error?.Code);
        Assert.IsNull(form.Selected);
        Assert.AreEqual(1, server.ListReads);
    }

    [TestMethod]
    public async Task ALostResponseIsReportedAsUnconfirmedAndTheListIsReadAgain()
    {
        var server = new ScriptedServer { DropNextWrite = true };
        var form = server.Form();
        form.BeginEdit(Shift);
        await form.ReviewAsync();

        Assert.IsFalse(await form.ConfirmAsync());

        Assert.AreEqual("OUTCOME_UNKNOWN", form.Error?.Code, "The page cannot know the change was not saved.");
        Assert.IsNull(form.Confirmation);
        Assert.AreEqual(1, server.ListReads);
    }

    [TestMethod]
    public async Task ASaveTheServerDidNotRecordKeepsTheChangeOpenToRetry()
    {
        var server = new ScriptedServer
        {
            Patch = (HttpStatusCode.ServiceUnavailable, ApiResponse.Failure<ManagedShift>(new ApiError(
                ApiErrorCategory.Unavailable, "NOT_RECORDED", "The change was not saved. Try again."))),
        };
        var form = server.Form();
        form.BeginEdit(Shift);
        form.RequiredCount = 4;
        await form.ReviewAsync();

        Assert.IsFalse(await form.ConfirmAsync());

        Assert.AreEqual("NOT_RECORDED", form.Error?.Code);
        Assert.AreEqual(Shift, form.Selected);
        Assert.AreEqual(4, form.RequiredCount);
    }

    [TestMethod]
    public void CancelClosesTheChange()
    {
        var form = new ScriptedServer().Form();
        form.BeginEdit(Shift);

        form.Cancel();

        Assert.IsNull(form.Selected);
        Assert.IsNull(form.Impact);
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        private int tokens;

        public (HttpStatusCode, ApiResponse<ShiftListResponse>) List { get; set; } =
            (HttpStatusCode.OK, ApiResponse.Success(new ShiftListResponse([Shift])));

        public (HttpStatusCode, ApiResponse<ShiftImpactResponse>) Impact { get; set; } =
            (HttpStatusCode.OK, ApiResponse.Success(Allowed));

        public (HttpStatusCode, ApiResponse<ManagedShift>) Patch { get; set; } =
            (HttpStatusCode.OK, ApiResponse.Success(Shift));

        public bool DropNextWrite { get; set; }
        public int Writes { get; private set; }
        public int ListReads { get; private set; }
        public string LastImpactQuery { get; private set; } = string.Empty;
        public string? LastDeleteQuery { get; private set; }
        public ChangeShiftRequest? LastPatch { get; private set; }
        public string? LastToken { get; private set; }

        public ShiftChangeForm Form() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/auth/antiforgery")
            {
                return Json(HttpStatusCode.OK, ApiResponse.Success(new AntiforgeryTokenResponse($"token-{++tokens}")));
            }

            if (path == "/api/v1/admin/shifts")
            {
                ListReads++;
                return Json(List.Item1, List.Item2);
            }

            if (path.EndsWith("/impact", StringComparison.Ordinal))
            {
                LastImpactQuery = request.RequestUri.Query;
                return Json(Impact.Item1, Impact.Item2);
            }

            Writes++;
            LastToken = request.Headers.TryGetValues("RequestVerificationToken", out var values) ? values.Single() : null;
            if (DropNextWrite)
            {
                DropNextWrite = false;
                throw new HttpRequestException("Connection dropped.");
            }

            if (request.Method == HttpMethod.Delete)
            {
                LastDeleteQuery = request.RequestUri.Query;
                return Json(HttpStatusCode.OK, ApiResponse.Success(new ShiftRemovedResponse(Shift.ShiftId)));
            }

            LastPatch = await request.Content!.ReadFromJsonAsync<ChangeShiftRequest>(cancellationToken);
            return Json(Patch.Item1, Patch.Item2);
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, ApiResponse<T> body) =>
            new(status) { Content = JsonContent.Create(body) };
    }
}
