using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Volunteer;

namespace ReFrog.Server.Tests.Administration;

// UC-ADM-manage-shifts 6a1: ADM.md test rows "6a" (preview), "6a1 affected volunteers", and "6a stale version".
// Sample data only.
[TestClass]
public sealed class AdminShiftImpactEndpointTests
{
    private const string Admin = "admin@example.org";
    private const string Unresolved = "ASSIGNED_SHIFT_CHANGE_UNRESOLVED";

    [TestMethod]
    public async Task PreviewOfMovingAHeldShiftNamesItsVolunteersAndChangesNothing()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        await SeedVolunteerAsync(fixture, shift, "approved@example.org", AssignmentStatus.Active);
        await SeedVolunteerAsync(fixture, shift, "pending@example.org", AssignmentStatus.Pending);
        await SeedVolunteerAsync(fixture, shift, "cancelled@example.org", AssignmentStatus.Cancelled);
        await SeedVolunteerAsync(fixture, shift, "denied@example.org", AssignmentStatus.Denied);
        using var client = await fixture.SignedInClientAsync(Admin);

        var impact = await ImpactAsync(client, shift, EditQuery(fixture, shift, date: "2027-05-05"));

        Assert.IsFalse(impact.Permitted);
        Assert.AreEqual(Unresolved, impact.RefusalCode);
        Assert.AreEqual(2, impact.HeldCount);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new AffectedVolunteer("approved@example.org", AssignmentStatus.Active),
                new AffectedVolunteer("pending@example.org", AssignmentStatus.Pending),
            },
            impact.AffectedVolunteers.ToArray());
        var stored = (await fixture.ShiftsAsync()).Single();
        Assert.AreEqual(shift.StartsAt, stored.StartsAt);
        CollectionAssert.AreEqual(shift.Version, stored.Version);
        Assert.IsEmpty(await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task PreviewOfRemovingAHeldShiftNamesItsVolunteersAndIsNotPermitted()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        await SeedVolunteerAsync(fixture, shift, "approved@example.org", AssignmentStatus.Active);
        using var client = await fixture.SignedInClientAsync(Admin);

        var impact = await ImpactAsync(client, shift, RemoveQuery(shift));

        Assert.IsFalse(impact.Permitted);
        Assert.AreEqual(Unresolved, impact.RefusalCode);
        Assert.AreEqual("approved@example.org", impact.AffectedVolunteers.Single().Email);
    }

    [TestMethod]
    public async Task ACountChangeOnAHeldShiftIsPermittedDownToTheHeldNumber()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync(requiredCount: 3);
        await SeedVolunteerAsync(fixture, shift, "approved@example.org", AssignmentStatus.Active);
        await SeedVolunteerAsync(fixture, shift, "pending@example.org", AssignmentStatus.Pending);
        using var client = await fixture.SignedInClientAsync(Admin);

        var raised = await ImpactAsync(client, shift, EditQuery(fixture, shift, count: 5));
        var atHeld = await ImpactAsync(client, shift, EditQuery(fixture, shift, count: 2));
        var belowHeld = await ImpactAsync(client, shift, EditQuery(fixture, shift, count: 1));

        Assert.IsTrue(raised.Permitted);
        Assert.IsTrue(atHeld.Permitted);
        Assert.IsNull(atHeld.RefusalCode);
        Assert.IsFalse(belowHeld.Permitted);
        Assert.AreEqual("CAPACITY_BELOW_HELD", belowHeld.RefusalCode);
    }

    [TestMethod]
    public async Task AShiftWithNoAssignmentsMayBeEditedAndRemoved()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);

        var edit = await ImpactAsync(client, shift, EditQuery(fixture, shift, date: "2027-05-05"));
        var removal = await ImpactAsync(client, shift, RemoveQuery(shift));

        Assert.IsTrue(edit.Permitted);
        Assert.IsEmpty(edit.AffectedVolunteers);
        Assert.IsTrue(removal.Permitted);
        Assert.AreEqual(0, removal.HeldCount);
    }

    [TestMethod]
    public async Task AssignmentHistoryBlocksRemovalButNotAnEdit()
    {
        foreach (var status in new[] { AssignmentStatus.Cancelled, AssignmentStatus.Denied })
        {
            await using var fixture = await AdminShiftFixture.CreateAsync();
            await fixture.CreateAccountAsync(Admin, isAdministrator: true);
            var shift = await fixture.SeedShiftAsync();
            await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), status);
            using var client = await fixture.SignedInClientAsync(Admin);

            var edit = await ImpactAsync(client, shift, EditQuery(fixture, shift, date: "2027-05-05"));
            var removal = await ImpactAsync(client, shift, RemoveQuery(shift));

            Assert.IsTrue(edit.Permitted, status);
            Assert.IsFalse(removal.Permitted, status);
            Assert.AreEqual(Unresolved, removal.RefusalCode, status);
            Assert.IsEmpty(removal.AffectedVolunteers, status);
        }
    }

    [TestMethod]
    public async Task TheAffectedVersionChangesOnlyWhenTheHeldVolunteersDo()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);

        var before = await ImpactAsync(client, shift, RemoveQuery(shift));
        var again = await ImpactAsync(client, shift, EditQuery(fixture, shift, count: 5));
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Cancelled);
        var afterHistory = await ImpactAsync(client, shift, RemoveQuery(shift));
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Pending);
        var afterSignUp = await ImpactAsync(client, shift, RemoveQuery(shift));

        Assert.AreEqual(before.AffectedVersion, again.AffectedVersion);
        Assert.AreEqual(before.AffectedVersion, afterHistory.AffectedVersion, "A cancelled sign-up affects nobody.");
        Assert.AreNotEqual(before.AffectedVersion, afterSignUp.AffectedVersion);
    }

    [TestMethod]
    public async Task AHeldVolunteerWhoseAccountIsGoneIsStillCounted()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        using var client = await fixture.SignedInClientAsync(Admin);

        var impact = await ImpactAsync(client, shift, RemoveQuery(shift));

        Assert.AreEqual(1, impact.HeldCount);
        Assert.AreEqual(new AffectedVolunteer(null, AssignmentStatus.Active), impact.AffectedVolunteers.Single());
    }

    [TestMethod]
    public async Task SignedOutAndNonAdministratorCallersSeeNoVolunteers()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync("volunteer@example.org", isAdministrator: false);
        var shift = await fixture.SeedShiftAsync();
        await SeedVolunteerAsync(fixture, shift, "approved@example.org", AssignmentStatus.Active);
        using var anonymous = fixture.Host.CreateClient();
        using var volunteer = await fixture.SignedInClientAsync("volunteer@example.org");

        foreach (var (client, status, code) in new[]
                 {
                     (anonymous, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"),
                     (volunteer, HttpStatusCode.Forbidden, "ADMINISTRATOR_REQUIRED"),
                 })
        {
            using var response = await client.GetAsync(ImpactUri(shift.Id, RemoveQuery(shift)));

            Assert.AreEqual(status, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            StringAssert.Contains(text, code, StringComparison.Ordinal);
            Assert.IsFalse(text.Contains("approved@example.org", StringComparison.OrdinalIgnoreCase));
        }
    }

    [TestMethod]
    public async Task AnOutOfDateVersionIsRefusedAndABadOneNamesTheField()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);

        await AssertRefusedAsync(client, shift.Id, "action=remove&expectedVersion=00000000000007D0",
            HttpStatusCode.Conflict, "SHIFT_VERSION_STALE");
        await AssertRefusedAsync(client, shift.Id, "action=remove",
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "expectedVersion");
        await AssertRefusedAsync(client, shift.Id, "action=remove&expectedVersion=not-a-version",
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "expectedVersion");
    }

    [TestMethod]
    public async Task UnknownShiftsAndShiftsOutsideTheCurrentEventAreNotFound()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var other = await fixture.SeedShiftAsync(eventId: Guid.NewGuid());
        using var client = await fixture.SignedInClientAsync(Admin);

        await AssertRefusedAsync(client, Guid.NewGuid(), RemoveQuery(other), HttpStatusCode.NotFound, "NOT_FOUND");
        await AssertRefusedAsync(client, other.Id, RemoveQuery(other), HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [TestMethod]
    public async Task BadPreviewDetailsNameTheirField()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var version = Convert.ToHexString(shift.Version);

        await AssertRefusedAsync(client, shift.Id, $"action=move&expectedVersion={version}",
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "action");
        await AssertRefusedAsync(client, shift.Id, $"action=edit&expectedVersion={version}&locationId=nowhere",
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "locationId");
        await AssertRefusedAsync(client, shift.Id, $"action=edit&expectedVersion={version}&requiredCount=many",
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "requiredCount");
        await AssertRefusedAsync(client, shift.Id, EditQuery(fixture, shift, count: 0),
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "requiredCount");
        await AssertRefusedAsync(client, shift.Id,
            $"action=edit&expectedVersion={version}&locationId={fixture.Commons}&date=2027-05-03&startTime=14:00&endTime=18:00",
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "requiredCount");
        await AssertRefusedAsync(client, shift.Id, EditQuery(fixture, shift, date: "2027-05-05", end: "21:00"),
            HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED", "endTime");
    }

    [TestMethod]
    public async Task UnreadableAssignmentsAreUnavailable()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
        {
            services.RemoveAll<IAssignmentQueryService>();
            services.AddScoped<IAssignmentQueryService, UnreadableAssignments>();
        });
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);

        await AssertRefusedAsync(client, shift.Id, RemoveQuery(shift), HttpStatusCode.ServiceUnavailable, "UNAVAILABLE");
    }

    private static async Task SeedVolunteerAsync(AdminShiftFixture fixture, Shift shift, string email, string status)
    {
        var accountId = await fixture.CreateAccountAsync(email, isAdministrator: false);
        await fixture.SeedAssignmentAsync(shift.Id, accountId, status);
    }

    private static string RemoveQuery(Shift shift) => $"action=remove&expectedVersion={Convert.ToHexString(shift.Version)}";

    /// <summary>An edit of the seeded shift (Monday May 3, 2:00–6:00 p.m.) with the given details changed.</summary>
    private static string EditQuery(
        AdminShiftFixture fixture, Shift shift, string date = "2027-05-03", string end = "18:00", int? count = null) =>
        $"action=edit&expectedVersion={Convert.ToHexString(shift.Version)}&locationId={fixture.Commons}" +
        $"&date={date}&startTime=14:00&endTime={end}" + $"&requiredCount={count ?? shift.RequiredCount}";

    private static Uri ImpactUri(Guid shiftId, string query) => new($"/api/v1/admin/shifts/{shiftId}/impact?{query}", UriKind.Relative);

    private static async Task<ShiftImpactResponse> ImpactAsync(HttpClient client, Shift shift, string query)
    {
        using var response = await client.GetAsync(ImpactUri(shift.Id, query));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ShiftImpactResponse>>())!.Data!;
    }

    private static async Task AssertRefusedAsync(
        HttpClient client, Guid shiftId, string query, HttpStatusCode status, string code, string? field = null)
    {
        using var response = await client.GetAsync(ImpactUri(shiftId, query));

        Assert.AreEqual(status, response.StatusCode, query);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftImpactResponse>>();
        Assert.AreEqual(code, body?.Error?.Code, query);
        Assert.IsNull(body?.Data, query);
        if (field is not null)
        {
            Assert.IsNotNull(body?.Error?.Fields?.GetValueOrDefault(field), $"{query} should name {field}.");
        }
    }
}
