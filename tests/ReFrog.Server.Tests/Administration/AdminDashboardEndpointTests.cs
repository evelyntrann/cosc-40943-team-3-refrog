using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Volunteer;

namespace ReFrog.Server.Tests.Administration;

// UC-ADM-view-dashboard: ADM.md test rows main, 2a, 3a, "Dashboard source outage", "Dashboard status accuracy", and
// "Audit/privacy". Volunteer coverage only; donation and shopping wait for DON and SHP (issue #136). Sample data only.
[TestClass]
public sealed class AdminDashboardEndpointTests
{
    private const string Admin = "admin@example.org";
    private const string Volunteer = "volunteer@example.org";
    private static readonly Uri DashboardUri = new("/api/v1/admin/dashboard", UriKind.Relative);

    [TestMethod]
    public async Task AdministratorSeesEveryLocationWithItsVolunteerCoverage()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shortShift = await fixture.SeedShiftAsync(requiredCount: 3);
        var fullShift = await fixture.SeedShiftAsync(requiredCount: 1);
        var otherEvent = await fixture.SeedShiftAsync(requiredCount: 5, eventId: Guid.NewGuid());
        await fixture.SeedAssignmentAsync(shortShift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        await fixture.SeedAssignmentAsync(shortShift.Id, Guid.NewGuid(), AssignmentStatus.Pending);
        await fixture.SeedAssignmentAsync(shortShift.Id, Guid.NewGuid(), AssignmentStatus.Cancelled);
        await fixture.SeedAssignmentAsync(shortShift.Id, Guid.NewGuid(), AssignmentStatus.Denied);
        await fixture.SeedAssignmentAsync(fullShift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        await fixture.SeedAssignmentAsync(otherEvent.Id, Guid.NewGuid(), AssignmentStatus.Active);
        using var client = await fixture.SignedInClientAsync(Admin);

        using var response = await client.GetAsync(DashboardUri);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var locations = (await response.Content.ReadFromJsonAsync<ApiResponse<DashboardResponse>>())!.Data!.Locations;
        CollectionAssert.AreEqual(
            new[] { fixture.ClosedPod, fixture.Commons, fixture.Village }, locations.Select(x => x.LocationId).ToArray(),
            "Every location, in INF's order by name.");
        var commons = locations.Single(x => x.LocationId == fixture.Commons);
        Assert.AreEqual(new DashboardCoverage(4, 2, 1, 3, 1), commons.Coverage, "The other event's shift is not counted.");
        Assert.IsTrue(commons.HasActivity);
        Assert.IsTrue(commons.HasUnfilledShifts, "Step 4: a location with an unfilled shift is highlighted.");
        Assert.HasCount(2, commons.Shifts);
        var shortSummary = commons.Shifts.Single(x => x.ShiftId == shortShift.Id);
        Assert.AreEqual(new DashboardCoverage(3, 1, 1, 2, 1), shortSummary.Coverage,
            "One approved and one pending hold slots; cancelled and denied do not.");
        Assert.IsTrue(shortSummary.Unfilled);
        Assert.AreEqual(shortShift.StartsAt, shortSummary.StartsAt);
        Assert.AreEqual(shortShift.EndsAt, shortSummary.EndsAt);
        var fullSummary = commons.Shifts.Single(x => x.ShiftId == fullShift.Id);
        Assert.AreEqual(new DashboardCoverage(1, 1, 0, 1, 0), fullSummary.Coverage);
        Assert.IsFalse(fullSummary.Unfilled);
    }

    [TestMethod]
    public async Task LocationsWithNoActivityStayOnTheDashboard()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.SeedShiftAsync(fixture.Village, requiredCount: 2);
        var released = await fixture.SeedShiftAsync(requiredCount: 1);
        await fixture.SeedAssignmentAsync(released.Id, Guid.NewGuid(), AssignmentStatus.Cancelled);
        using var client = await fixture.SignedInClientAsync(Admin);

        var locations = (await client.GetFromJsonAsync<ApiResponse<DashboardResponse>>(DashboardUri))!.Data!.Locations;

        Assert.HasCount(3, locations, "3a: no location is left out.");
        var closed = locations.Single(x => x.LocationId == fixture.ClosedPod);
        Assert.AreEqual("CLOSED", closed.Status);
        Assert.IsFalse(closed.HasActivity);
        Assert.IsFalse(closed.HasUnfilledShifts, "A location with no shifts has nothing unfilled.");
        Assert.AreEqual(new DashboardCoverage(0, 0, 0, 0, 0), closed.Coverage);
        Assert.IsEmpty(closed.Shifts);
        var village = locations.Single(x => x.LocationId == fixture.Village);
        Assert.IsFalse(village.HasActivity);
        Assert.IsTrue(village.HasUnfilledShifts);
        Assert.AreEqual(new DashboardCoverage(2, 0, 0, 0, 2), village.Coverage);
        var commons = locations.Single(x => x.LocationId == fixture.Commons);
        Assert.IsFalse(commons.HasActivity, "A cancelled sign-up is not current activity.");
        Assert.AreEqual(new DashboardCoverage(1, 0, 0, 0, 1), commons.Coverage);
    }

    [TestMethod]
    public async Task SignedOutAndNonAdministratorCallersGetNoDashboard()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Volunteer, isAdministrator: false);
        await fixture.SeedShiftAsync();
        using var anonymous = fixture.Host.CreateClient();
        using var volunteer = await fixture.SignedInClientAsync(Volunteer);

        foreach (var (client, status, code) in new[]
                 {
                     (anonymous, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"),
                     (volunteer, HttpStatusCode.Forbidden, "ADMINISTRATOR_REQUIRED"),
                 })
        {
            using var response = await client.GetAsync(DashboardUri);

            Assert.AreEqual(status, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardResponse>>();
            Assert.AreEqual(code, body?.Error?.Code);
            Assert.AreEqual(ApiErrorCategory.Authorization, body?.Error?.Category);
            Assert.IsNull(body?.Data);
        }
    }

    [TestMethod]
    public async Task NoCurrentEventIsRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(withEvent: false);
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);

        using var response = await client.GetAsync(DashboardUri);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardResponse>>();
        Assert.AreEqual("NO_CURRENT_EVENT", body?.Error?.Code);
        Assert.IsNull(body?.Data);
    }

    [TestMethod]
    public async Task UnreadableAssignmentsMakeTheDashboardUnavailableNotZero()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
        {
            services.RemoveAll<IAssignmentQueryService>();
            services.AddScoped<IAssignmentQueryService, UnreadableAssignments>();
        });
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);

        using var response = await client.GetAsync(DashboardUri);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardResponse>>();
        Assert.AreEqual("UNAVAILABLE", body?.Error?.Code);
        Assert.AreEqual(ApiErrorCategory.Unavailable, body?.Error?.Category);
        Assert.IsNull(body?.Data, "FR-DISPLAY-unavailable-data: no partial or zero totals.");
    }

    [TestMethod]
    public async Task TheDashboardNamesNoVolunteer()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var volunteerId = await fixture.CreateAccountAsync(Volunteer, isAdministrator: false);
        var shift = await fixture.SeedShiftAsync();
        await fixture.SeedAssignmentAsync(shift.Id, volunteerId, AssignmentStatus.Active);
        using var client = await fixture.SignedInClientAsync(Admin);

        var text = await client.GetStringAsync(DashboardUri);

        Assert.Contains("\"activeCount\":1", text);
        Assert.DoesNotContain(volunteerId.ToString(), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Volunteer, text, StringComparison.OrdinalIgnoreCase);
    }
}
