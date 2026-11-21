using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Info.Persistence;

namespace ReFrog.Server.Tests.Administration;

// UC-ADM-manage-shifts, first build: ADM.md test rows "main", "2a", "Admin write antiforgery", "4a", "4b",
// "duplicate submission", "no current event", and "Location default count". Sample data only.
[TestClass]
public sealed class AdminShiftEndpointTests
{
    private const string Admin = "admin@example.org";
    private const string Volunteer = "volunteer@example.org";

    [TestMethod]
    public async Task AdministratorCreatesAShiftThatOpensForSignup()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var adminId = await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        using var staffing = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, 3, token);
        Assert.AreEqual(HttpStatusCode.OK, staffing.StatusCode);

        using var response = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(), token);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())!.Data!;
        // 2:00–6:00 p.m. CDT on May 3, 2027 is 19:00–23:00 UTC (ADM.md key decision 12).
        Assert.AreEqual(new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero), created.StartsAt);
        Assert.AreEqual(new DateTimeOffset(2027, 5, 3, 23, 0, 0, TimeSpan.Zero), created.EndsAt);
        Assert.AreEqual(3, created.RequiredCount, "Omitted count takes the location's default.");
        var stored = (await fixture.ShiftsAsync()).Single();
        Assert.AreEqual(created.ShiftId, stored.Id);
        Assert.AreEqual(fixture.EventId, stored.EventId);
        Assert.AreEqual(adminId, stored.CreatedByAccountId);
        var shiftAudit = (await fixture.AuditAsync()).Where(x => x.Action == "SHIFT_CREATED").ToList();
        Assert.HasCount(1, shiftAudit);
        Assert.AreEqual(adminId, shiftAudit[0].ActorAccountId);
        Assert.AreEqual(created.ShiftId.ToString(), shiftAudit[0].TargetId);

        // POST-1: the shift is visible to volunteers through VOL's ShiftService contract.
        await using var scope = fixture.Host.Factory.Services.CreateAsyncScope();
        var candidates = await scope.ServiceProvider.GetRequiredService<ShiftService>()
            .FindSignupCandidatesAsync(fixture.Host.Clock.GetUtcNow(), CancellationToken.None);
        Assert.AreEqual(created.ShiftId, candidates.Single().ShiftId);
        Assert.AreEqual(3, candidates.Single().RequiredCount);
    }

    [TestMethod]
    public async Task ShiftOptionsShowTheCurrentEventItsLocationsHoursAndDefaults()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.SetDefaultDirectlyAsync(fixture.Commons, 3);
        using var client = await fixture.SignedInClientAsync(Admin);

        var options = (await client.GetFromJsonAsync<ApiResponse<ShiftOptionsResponse>>("/api/v1/admin/shift-options"))!.Data!;

        Assert.AreEqual(fixture.EventId, options.Event.EventId);
        Assert.AreEqual(new DateOnly(2027, 5, 3), options.Event.StartDate);
        Assert.AreEqual(new DateOnly(2027, 5, 8), options.Event.EndDate);
        Assert.AreEqual("Closed Pod|Commons|Village", string.Join('|', options.Locations.Select(x => x.Name)));
        var commons = options.Locations.Single(x => x.LocationId == fixture.Commons);
        Assert.AreEqual(3, commons.DefaultRequiredCount);
        Assert.AreEqual(new LocationHours(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(20, 0)), commons.OperatingHours[0]);
        Assert.IsNull(options.Locations.Single(x => x.LocationId == fixture.Village).DefaultRequiredCount);
        Assert.AreEqual(DonationLocationStatus.Closed, options.Locations.Single(x => x.LocationId == fixture.ClosedPod).Status);
    }

    [TestMethod]
    public async Task SignedOutAndNonAdministratorCallersAreRefusedAndNothingIsStored()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Volunteer, isAdministrator: false);
        using var anonymous = fixture.Host.CreateClient();
        using var volunteer = await fixture.SignedInClientAsync(Volunteer);
        var anonymousToken = await AdminShiftFixture.TokenAsync(anonymous);
        var volunteerToken = await AdminShiftFixture.TokenAsync(volunteer);

        foreach (var (client, token, status, code) in new[]
                 {
                     (anonymous, anonymousToken, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"),
                     (volunteer, volunteerToken, HttpStatusCode.Forbidden, "ADMINISTRATOR_REQUIRED"),
                 })
        {
            using var options = await client.GetAsync(new Uri("/api/v1/admin/shift-options", UriKind.Relative));
            using var create = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(2), token);
            using var staffing = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, 3, token);
            foreach (var response in new[] { options, create, staffing })
            {
                Assert.AreEqual(status, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
                Assert.AreEqual(code, body?.Error?.Code);
                Assert.AreEqual(ApiErrorCategory.Authorization, body?.Error?.Category);
                Assert.IsNull(body?.Data);
            }
        }

        Assert.IsEmpty(await fixture.ShiftsAsync());
        Assert.AreEqual(0, await fixture.StaffingCountAsync());
        Assert.IsEmpty(await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task AdministratorWhoseRoleWasRemovedIsRefusedOnTheNextRequest()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        await using (var scope = fixture.Host.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
            var account = await users.FindByEmailAsync(Admin);
            Assert.IsTrue((await users.RemoveFromRoleAsync(account!, SessionService.AdministratorRole)).Succeeded);
        }

        using var response = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(2), token);
        using var list = await client.GetAsync(new Uri("/api/v1/admin/shifts", UriKind.Relative));
        using var impact = await client.GetAsync(new Uri($"/api/v1/admin/shifts/{Guid.NewGuid()}/impact?action=remove", UriKind.Relative));
        using var remove = new HttpRequestMessage(HttpMethod.Delete, new Uri($"/api/v1/admin/shifts/{Guid.NewGuid()}", UriKind.Relative));
        remove.Headers.Add("RequestVerificationToken", token);
        using var removed = await client.SendAsync(remove);
        using var dashboard = await client.GetAsync(new Uri("/api/v1/admin/dashboard", UriKind.Relative));

        foreach (var refused in new[] { response, list, impact, removed, dashboard })
        {
            Assert.IsTrue(refused.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
                $"Expected a refusal, got {(int)refused.StatusCode}.");
        }

        Assert.IsEmpty(await fixture.ShiftsAsync());
    }

    [TestMethod]
    public async Task UnreadableRoleStateIsUnavailableNotAGuess()
    {
        var roleChecks = new RoleCheckSwitch();
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
        {
            services.AddSingleton(roleChecks);
            services.RemoveAll<UserManager<AuthAccount>>();
            services.AddScoped<UserManager<AuthAccount>, FlakyUserManager>();
        });
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        roleChecks.Fail = true;

        using var options = await client.GetAsync(new Uri("/api/v1/admin/shift-options", UriKind.Relative));
        using var create = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(2), token);
        using var list = await client.GetAsync(new Uri("/api/v1/admin/shifts", UriKind.Relative));
        using var impact = await client.GetAsync(new Uri($"/api/v1/admin/shifts/{Guid.NewGuid()}/impact?action=remove", UriKind.Relative));
        using var remove = new HttpRequestMessage(HttpMethod.Delete, new Uri($"/api/v1/admin/shifts/{Guid.NewGuid()}", UriKind.Relative));
        remove.Headers.Add("RequestVerificationToken", token);
        using var removed = await client.SendAsync(remove);
        using var dashboard = await client.GetAsync(new Uri("/api/v1/admin/dashboard", UriKind.Relative));

        foreach (var response in new[] { options, create, list, impact, removed, dashboard })
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.AreEqual("AUTHENTICATION_UNAVAILABLE", body?.Error?.Code);
            Assert.AreEqual(ApiErrorCategory.Unavailable, body?.Error?.Category);
        }

        Assert.IsEmpty(await fixture.ShiftsAsync());
    }

    [TestMethod]
    public async Task AdministratorWritesWithoutAValidAntiforgeryTokenAreRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);

        using var create = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(2), token: null);
        using var forged = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(2), token: "forged");
        using var staffing = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, 3, token: null);

        foreach (var response in new[] { create, forged, staffing })
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("ANTIFORGERY_INVALID", (await response.Content.ReadFromJsonAsync<ApiResponse<object>>())?.Error?.Code);
        }

        Assert.IsEmpty(await fixture.ShiftsAsync());
        Assert.AreEqual(0, await fixture.StaffingCountAsync());
        Assert.IsEmpty(await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task InvalidDetailsNameOnlyTheirFieldsAndStoreNothing()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.SetDefaultDirectlyAsync(fixture.Commons, 3);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        var valid = fixture.ValidRequest();

        var cases = new (string Case, CreateShiftRequest Request, string[] Fields)[]
        {
            ("zero count", valid with { RequiredCount = 0 }, ["requiredCount"]),
            ("negative count", valid with { RequiredCount = -2 }, ["requiredCount"]),
            ("no location", valid with { LocationId = null }, ["locationId"]),
            ("unknown location", valid with { LocationId = Guid.NewGuid() }, ["locationId"]),
            ("closed location", valid with { LocationId = fixture.ClosedPod }, ["locationId"]),
            ("before the event", valid with { Date = "2027-05-02" }, ["date"]),
            ("after the event", valid with { Date = "2027-05-09" }, ["date"]),
            ("weekday with no hours", valid with { Date = "2027-05-04" }, ["date"]),
            ("starts before opening", valid with { StartTime = "13:00" }, ["startTime"]),
            ("ends after closing", valid with { EndTime = "21:00" }, ["endTime"]),
            ("end not after start", valid with { StartTime = "18:00", EndTime = "14:00" }, ["endTime"]),
            ("malformed time", valid with { StartTime = "25:00" }, ["startTime"]),
            ("start before opening and no end", valid with { StartTime = "09:00", EndTime = null }, ["endTime", "startTime"]),
            ("malformed date", valid with { Date = "05/03/2027" }, ["date"]),
            ("no count and no default", valid with { LocationId = fixture.Village, EndTime = "17:00" }, ["requiredCount"]),
            // 2:30 a.m. on Sunday March 14, 2027 is skipped by the spring-forward change. The date is also
            // outside the event and a Sunday, so the date is named too; the start time names the skipped time.
            ("time skipped by daylight saving", valid with { Date = "2027-03-14", StartTime = "02:30", EndTime = "04:00" },
                ["date", "startTime"]),
        };

        foreach (var (name, request, expected) in cases)
        {
            using var response = await AdminShiftFixture.PostShiftAsync(client, request, token);

            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode, name);
            var error = (await response.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())?.Error;
            Assert.AreEqual("VALIDATION_FAILED", error?.Code, name);
            Assert.AreEqual(string.Join('|', expected.Order()), string.Join('|', error!.Fields!.Keys.Order()),
                $"{name}: only the invalid fields are named; valid input is left for the form to keep.");
        }

        var skipped = await AdminShiftFixture.PostShiftAsync(client, cases[^1].Request, token);
        StringAssert.Contains((await skipped.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())!.Error!.Fields!["startTime"],
            "daylight-saving", StringComparison.Ordinal);
        Assert.IsEmpty(await fixture.ShiftsAsync());
        Assert.IsEmpty(await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task StoreFailureReportsNotRecordedAndARetryCreatesExactlyOneShift()
    {
        var failure = new FailNextSave();
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
            services.ConfigureDbContext<AdmDbContext>((_, options) => options.AddInterceptors(failure)));
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        var request = fixture.ValidRequest(2);
        failure.Armed = true;

        using var failed = await AdminShiftFixture.PostShiftAsync(client, request, token);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var error = (await failed.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())?.Error;
        Assert.AreEqual("NOT_RECORDED", error?.Code);
        Assert.AreEqual(ApiErrorCategory.Unavailable, error?.Category);
        Assert.IsTrue(failure.Fired, "The failure happened after the inserts ran inside the transaction.");
        Assert.IsEmpty(await fixture.ShiftsAsync(), "The shift insert was rolled back.");
        Assert.IsEmpty(await fixture.AuditAsync(), "The audit insert was rolled back with it.");

        using var retried = await AdminShiftFixture.PostShiftAsync(client, request, token);

        Assert.AreEqual(HttpStatusCode.Created, retried.StatusCode);
        Assert.HasCount(1, await fixture.ShiftsAsync());
        Assert.HasCount(1, await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task SameRequestIdGivesTheSameShiftAndChangedInputIsRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        var request = fixture.ValidRequest(2);

        using var first = await AdminShiftFixture.PostShiftAsync(client, request, token);
        using var again = await AdminShiftFixture.PostShiftAsync(client, request, token);
        using var changed = await AdminShiftFixture.PostShiftAsync(client, request with { EndTime = "19:00" }, token);

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, again.StatusCode);
        Assert.AreEqual(await first.Content.ReadAsStringAsync(), await again.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.AreEqual("REQUEST_ID_REUSED", (await changed.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())?.Error?.Code);
        Assert.HasCount(1, await fixture.ShiftsAsync());
        Assert.HasCount(1, await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task ARetryReturnsItsShiftEvenAfterTheSetupChanged()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.SetDefaultDirectlyAsync(fixture.Commons, 3);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        var request = fixture.ValidRequest();
        using var first = await AdminShiftFixture.PostShiftAsync(client, request, token);
        var created = (await first.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())!.Data!;

        // Between the lost response and the retry: the location closes, its default changes, the event closes.
        await fixture.Host.WithAsync<InfoDbContext>(async db =>
        {
            (await db.Locations.SingleAsync(x => x.Id == fixture.Commons)).Status = DonationLocationStatus.Closed;
            (await db.Events.SingleAsync(x => x.Id == fixture.EventId)).Status = ReFrogEventStatus.Closed;
            await db.SaveChangesAsync();
        });
        await fixture.Host.WithAsync<AdmDbContext>(async db =>
        {
            (await db.LocationStaffing.SingleAsync()).DefaultRequiredCount = 5;
            await db.SaveChangesAsync();
        });
        using var retried = await AdminShiftFixture.PostShiftAsync(client, request, token);

        Assert.AreEqual(HttpStatusCode.Created, retried.StatusCode);
        Assert.AreEqual(created, (await retried.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())!.Data);
        Assert.HasCount(1, await fixture.ShiftsAsync());
    }

    [TestMethod]
    public async Task AValueOfTheWrongTypeIsNamedAsItsField()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        var requestId = Guid.NewGuid();

        var cases = new (string Route, HttpMethod Method, string Body, string? Field)[]
        {
            ("/api/v1/admin/shifts", HttpMethod.Post,
                $$"""{"requestId":"{{requestId}}","locationId":"abc","date":"2027-05-03","startTime":"14:00","endTime":"18:00"}""", "locationId"),
            ("/api/v1/admin/shifts", HttpMethod.Post,
                $$"""{"requestId":"{{requestId}}","locationId":"{{fixture.Commons}}","date":"2027-05-03","startTime":"14:00","endTime":"18:00","requiredCount":2.5}""",
                "requiredCount"),
            ($"/api/v1/admin/events/{fixture.EventId}/locations/{fixture.Commons}/staffing", HttpMethod.Put,
                """{"defaultRequiredCount":"three"}""", "defaultRequiredCount"),
            ("/api/v1/admin/shifts", HttpMethod.Post, "not json", null),
        };

        foreach (var (route, method, body, field) in cases)
        {
            using var message = new HttpRequestMessage(method, route)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
            message.Headers.Add("RequestVerificationToken", token);
            using var response = await client.SendAsync(message);

            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode, body);
            var fields = (await response.Content.ReadFromJsonAsync<ApiResponse<object>>())?.Error?.Fields;
            Assert.AreEqual(field, fields?.Keys.Single(), body);
        }

        Assert.IsEmpty(await fixture.ShiftsAsync());
    }

    [TestMethod]
    public async Task WithNoPlannedOrActiveEventOptionsAndCreateAreRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(withEvent: false);
        await fixture.Host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(Info.EventConfigServiceTests.Event(ReFrogEventStatus.Closed, 2026));
            return db.SaveChangesAsync();
        });
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var options = await client.GetAsync(new Uri("/api/v1/admin/shift-options", UriKind.Relative));
        using var create = await AdminShiftFixture.PostShiftAsync(client,
            new CreateShiftRequest(Guid.NewGuid(), Guid.NewGuid(), "2027-05-03", "14:00", "18:00", 2), token);

        foreach (var response in new[] { options, create })
        {
            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.AreEqual("NO_CURRENT_EVENT", (await response.Content.ReadFromJsonAsync<ApiResponse<object>>())?.Error?.Code);
        }

        Assert.IsEmpty(await fixture.ShiftsAsync());
    }

    [TestMethod]
    public async Task LocationDefaultAppliesToLaterShiftsOnlyAndEachChangeIsAudited()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var adminId = await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var setThree = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, 3, token);
        var saved = (await setThree.Content.ReadFromJsonAsync<ApiResponse<LocationStaffingResponse>>())?.Data;
        var fromDefault = await CreatedCountAsync(client, fixture.ValidRequest(), token);
        var overridden = await CreatedCountAsync(client, fixture.ValidRequest(2) with { StartTime = "15:00" }, token);
        using var setFour = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, 4, token);
        var afterChange = await CreatedCountAsync(client, fixture.ValidRequest() with { StartTime = "16:00" }, token);

        Assert.AreEqual(new LocationStaffingResponse(fixture.EventId, fixture.Commons, 3), saved);
        Assert.AreEqual(HttpStatusCode.OK, setFour.StatusCode);
        Assert.AreEqual(3, fromDefault);
        Assert.AreEqual(2, overridden, "An override changes only its own shift.");
        Assert.AreEqual(4, afterChange);
        Assert.AreEqual("2|3|4", string.Join('|', (await fixture.ShiftsAsync()).Select(x => x.RequiredCount).Order()),
            "Changing the default left the earlier shifts' counts unchanged.");
        var defaultAudits = (await fixture.AuditAsync()).Where(x => x.Action == "LOCATION_DEFAULT_SET").ToList();
        Assert.HasCount(2, defaultAudits);
        Assert.IsTrue(defaultAudits.All(x => x.ActorAccountId == adminId && x.TargetId == $"{fixture.EventId}/{fixture.Commons}"));
    }

    [TestMethod]
    public async Task StaffingRefusesUnknownEventOrLocationAndNonPositiveCount()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var unknownEvent = await AdminShiftFixture.PutStaffingAsync(client, Guid.NewGuid(), fixture.Commons, 3, token);
        using var unknownLocation = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, Guid.NewGuid(), 3, token);
        using var zero = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, 0, token);
        using var missing = await AdminShiftFixture.PutStaffingAsync(client, fixture.EventId, fixture.Commons, null, token);

        Assert.AreEqual(HttpStatusCode.NotFound, unknownEvent.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, unknownLocation.StatusCode);
        foreach (var response in new[] { zero, missing })
        {
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var fields = (await response.Content.ReadFromJsonAsync<ApiResponse<LocationStaffingResponse>>())?.Error?.Fields;
            Assert.AreEqual("defaultRequiredCount", fields?.Keys.Single());
        }

        Assert.AreEqual(0, await fixture.StaffingCountAsync());
        Assert.IsEmpty(await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task EventConfigurationOutageIsReportedNotGuessed()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
        {
            services.RemoveAll<IEventConfigStore>();
            services.AddScoped<IEventConfigStore, FailingEventConfigStore>();
        });
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var options = await client.GetAsync(new Uri("/api/v1/admin/shift-options", UriKind.Relative));
        using var create = await AdminShiftFixture.PostShiftAsync(client, fixture.ValidRequest(2), token);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, options.StatusCode);
        Assert.AreEqual("UNAVAILABLE", (await options.Content.ReadFromJsonAsync<ApiResponse<object>>())?.Error?.Code);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, create.StatusCode);
        Assert.AreEqual("NOT_RECORDED", (await create.Content.ReadFromJsonAsync<ApiResponse<object>>())?.Error?.Code);
        Assert.IsEmpty(await fixture.ShiftsAsync());
    }

    private static async Task<int> CreatedCountAsync(HttpClient client, CreateShiftRequest request, string token)
    {
        using var response = await AdminShiftFixture.PostShiftAsync(client, request, token);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ShiftResponse>>())!.Data!.RequiredCount;
    }

    private sealed class RoleCheckSwitch
    {
        public bool Fail { get; set; }
    }

    /// <summary>Role lookups fail on demand, as when the role store cannot be read.</summary>
    private sealed class FlakyUserManager(
        RoleCheckSwitch roleChecks,
        IUserStore<AuthAccount> store,
        IOptions<IdentityOptions> options,
        IPasswordHasher<AuthAccount> hasher,
        IEnumerable<IUserValidator<AuthAccount>> userValidators,
        IEnumerable<IPasswordValidator<AuthAccount>> passwordValidators,
        ILookupNormalizer normalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<AuthAccount>> logger)
        : UserManager<AuthAccount>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
    {
        public override Task<bool> IsInRoleAsync(AuthAccount user, string role) => roleChecks.Fail
            ? throw new InvalidOperationException("Simulated role store failure.")
            : base.IsInRoleAsync(user, role);
    }

    /// <summary>Fails one save after its commands ran, so only a rollback can leave nothing behind (4b).</summary>
    private sealed class FailNextSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public bool Fired { get; private set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!Armed)
            {
                return ValueTask.FromResult(result);
            }

            Armed = false;
            Fired = result > 0;
            throw new DbUpdateException("Simulated failure after the inserts ran.");
        }
    }

    private sealed class FailingEventConfigStore : IEventConfigStore
    {
        public Task<ConfiguredEvent?> FindCurrentEventAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated INF outage.");

        public Task<ConfiguredEvent?> LockCurrentEventAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated INF outage.");

        public Task<ConfiguredEvent?> FindEventAsync(Guid eventId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated INF outage.");

        public Task<IReadOnlyList<ConfiguredLocation>> ListLocationsAsync(Guid eventId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated INF outage.");
    }
}
