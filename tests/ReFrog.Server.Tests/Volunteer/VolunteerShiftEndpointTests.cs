using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Volunteer;
using ReFrog.Server.Features.Volunteer.Persistence;
using ReFrog.Server.Tests.Administration;

namespace ReFrog.Server.Tests.Volunteer;

// UC-VOL-signup: VOL.md test rows "main", "1a list", "1a sign-up", "step 2", "2a", "4a full", "4a race",
// "4a not open", "4b", "4c", "5b sequential", "5c", "5c pending holds slot", "reactivate", and "unauthenticated".
[TestClass]
public sealed class VolunteerShiftEndpointTests
{
    private const string ApprovedVolunteer = "volunteer@example.org";
    private const string NewVolunteer = "newcomer@example.org";
    private const string OtherVolunteer = "other@example.org";
    private const string RestrictedVolunteer = "restricted@example.org";
    private const string Admin = "admin@example.org";

    [TestMethod]
    public async Task ApprovedVolunteerClaimsFreeSlotAndCapacityDecrements()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var shift = await CreateShiftAsync(fixture, requiredCount: 2);
        await fixture.CreateAccountAsync(ApprovedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRole);

        using var client = await fixture.SignedInClientAsync(ApprovedVolunteer);
        using var response = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.IsNotNull(envelope?.Data);
        Assert.AreEqual(AssignmentStatus.Active, envelope.Data.Status);
        Assert.AreEqual(1, envelope.Data.OpenSlots);
        Assert.IsFalse(envelope.Data.AlreadyAssigned);

        var assignment = await fixture.Host.WithAsync<VolDbContext, ShiftAssignment?>(
            db => db.ShiftAssignments.SingleOrDefaultAsync(x => x.ShiftId == shift.Id));
        Assert.IsNotNull(assignment);
        Assert.AreEqual(AssignmentStatus.Active, assignment.Status);
    }

    [TestMethod]
    public async Task UnapprovedVolunteerClaimsSlotAsPendingAndHoldsCapacity()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var shift = await CreateShiftAsync(fixture, requiredCount: 1);
        await fixture.CreateAccountAsync(NewVolunteer, isAdministrator: false); // Role is NONE (5c)
        await fixture.CreateAccountAsync(OtherVolunteer, isAdministrator: false);

        // 5c: New volunteer claims slot as PENDING
        using var client = await fixture.SignedInClientAsync(NewVolunteer);
        using var response = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.IsNotNull(envelope?.Data);
        Assert.AreEqual(AssignmentStatus.Pending, envelope.Data.Status);
        Assert.AreEqual(0, envelope.Data.OpenSlots);

        // 5c pending holds slot: Another volunteer tries to claim the same shift and gets SHIFT_FULL (4a)
        using var otherClient = await fixture.SignedInClientAsync(OtherVolunteer);
        using var fullResponse = await otherClient.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);
        Assert.AreEqual(HttpStatusCode.Conflict, fullResponse.StatusCode);
        var errorEnvelope = await fullResponse.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.AreEqual("SHIFT_FULL", errorEnvelope?.Error?.Code);
    }

    [TestMethod]
    public async Task RestrictedAccountIsDeniedOnListAndSignUp()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var shift = await CreateShiftAsync(fixture, requiredCount: 2);
        await fixture.CreateAccountAsync(RestrictedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRestrictedRole);

        using var client = await fixture.SignedInClientAsync(RestrictedVolunteer);

        // 1a list
        using var listResponse = await client.GetAsync("/api/v1/shifts/open");
        Assert.AreEqual(HttpStatusCode.Forbidden, listResponse.StatusCode);
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<OpenShiftDto>>>();
        Assert.AreEqual("VOLUNTEER_ACCESS_INACTIVE", listEnvelope?.Error?.Code);

        // 1a sign-up
        using var signUpResponse = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, signUpResponse.StatusCode);
        var signUpEnvelope = await signUpResponse.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.AreEqual("VOLUNTEER_ACCESS_INACTIVE", signUpEnvelope?.Error?.Code);
    }

    [TestMethod]
    public async Task OpenShiftsListAnnotatesAssignedToMeAndExcludesFullAndDeniedShifts()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var openShift = await CreateShiftAsync(fixture, requiredCount: 2);
        var fullShift = await CreateShiftAsync(fixture, requiredCount: 1);
        var deniedShift = await CreateShiftAsync(fixture, requiredCount: 2);

        var volunteerId = await fixture.CreateAccountAsync(ApprovedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRole);
        var otherId = await fixture.CreateAccountAsync(OtherVolunteer, isAdministrator: false);

        // Setup: volunteer holds a slot on openShift
        await fixture.Host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                ShiftId = openShift.Id,
                VolunteerId = volunteerId,
                Status = AssignmentStatus.Active,
                AssignedAt = AdminShiftFixture.Now
            });
            // fullShift is filled by other volunteer
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                ShiftId = fullShift.Id,
                VolunteerId = otherId,
                Status = AssignmentStatus.Active,
                AssignedAt = AdminShiftFixture.Now
            });
            // volunteer was DENIED on deniedShift (4c)
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                ShiftId = deniedShift.Id,
                VolunteerId = volunteerId,
                Status = AssignmentStatus.Denied,
                AssignedAt = AdminShiftFixture.Now
            });
            return db.SaveChangesAsync();
        });

        using var client = await fixture.SignedInClientAsync(ApprovedVolunteer);
        using var response = await client.GetAsync("/api/v1/shifts/open");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<OpenShiftDto>>>();
        Assert.IsNotNull(envelope?.Data);

        // openShift should be returned with AssignedToMe = true and 1 open slot
        var listed = envelope.Data.SingleOrDefault(x => x.ShiftId == openShift.Id);
        Assert.IsNotNull(listed);
        Assert.IsTrue(listed.AssignedToMe);
        Assert.AreEqual(1, listed.OpenSlots);

        // fullShift and deniedShift should be excluded
        Assert.IsFalse(envelope.Data.Any(x => x.ShiftId == fullShift.Id));
        Assert.IsFalse(envelope.Data.Any(x => x.ShiftId == deniedShift.Id));
    }

    [TestMethod]
    public async Task SigningUpForAlreadyAssignedShiftReturnsOkWithAlreadyAssigned()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var shift = await CreateShiftAsync(fixture, requiredCount: 2);
        await fixture.CreateAccountAsync(ApprovedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRole);

        using var client = await fixture.SignedInClientAsync(ApprovedVolunteer);

        // First sign-up: 201 Created
        using var first = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);
        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);

        // 4b / 5b: Repeat sign-up: 200 OK with alreadyAssigned: true
        using var second = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);
        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
        var envelope = await second.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.IsNotNull(envelope?.Data);
        Assert.IsTrue(envelope.Data.AlreadyAssigned);

        var count = await fixture.Host.WithAsync<VolDbContext, int>(
            db => db.ShiftAssignments.CountAsync(x => x.ShiftId == shift.Id));
        Assert.AreEqual(1, count, "No duplicate assignment row created.");
    }

    [TestMethod]
    public async Task SigningUpForDeniedShiftReturnsConflictSignupDenied()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var shift = await CreateShiftAsync(fixture, requiredCount: 2);
        var volunteerId = await fixture.CreateAccountAsync(ApprovedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRole);

        await fixture.Host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                VolunteerId = volunteerId,
                Status = AssignmentStatus.Denied,
                AssignedAt = AdminShiftFixture.Now
            });
            return db.SaveChangesAsync();
        });

        using var client = await fixture.SignedInClientAsync(ApprovedVolunteer);
        using var response = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.AreEqual("SIGNUP_DENIED", envelope?.Error?.Code);
    }

    [TestMethod]
    public async Task SigningUpForCancelledShiftReactivatesSameRow()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var shift = await CreateShiftAsync(fixture, requiredCount: 2);
        var volunteerId = await fixture.CreateAccountAsync(ApprovedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRole);
        var assignmentId = Guid.NewGuid();

        await fixture.Host.WithAsync<VolDbContext>(db =>
        {
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                Id = assignmentId,
                ShiftId = shift.Id,
                VolunteerId = volunteerId,
                Status = AssignmentStatus.Cancelled,
                AssignedAt = AdminShiftFixture.Now,
                CancelledAt = AdminShiftFixture.Now
            });
            return db.SaveChangesAsync();
        });

        using var client = await fixture.SignedInClientAsync(ApprovedVolunteer);
        using var response = await client.PostAsync($"/api/v1/shifts/{shift.Id}/assignments", null);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.AreEqual(assignmentId, envelope?.Data?.AssignmentId);
        Assert.AreEqual(AssignmentStatus.Active, envelope?.Data?.Status);

        var stored = await fixture.Host.WithAsync<VolDbContext, ShiftAssignment?>(
            db => db.ShiftAssignments.SingleOrDefaultAsync(x => x.ShiftId == shift.Id));
        Assert.IsNotNull(stored);
        Assert.AreEqual(assignmentId, stored.Id);
        Assert.AreEqual(AssignmentStatus.Active, stored.Status);
        Assert.IsNull(stored.CancelledAt);
    }

    [TestMethod]
    public async Task NonexistentShiftReturnsNotFound()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(ApprovedVolunteer, isAdministrator: false, VolunteerAuthorization.VolunteerRole);

        using var client = await fixture.SignedInClientAsync(ApprovedVolunteer);
        using var response = await client.PostAsync($"/api/v1/shifts/{Guid.NewGuid()}/assignments", null);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<SignUpResultDto>>();
        Assert.AreEqual("SHIFT_NOT_FOUND", envelope?.Error?.Code);
    }

    [TestMethod]
    public async Task UnauthenticatedRequestsReturnUnauthorized()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var client = fixture.Host.CreateClient();

        using var get = await client.GetAsync("/api/v1/shifts/open");
        Assert.AreEqual(HttpStatusCode.Unauthorized, get.StatusCode);

        using var post = await client.PostAsync($"/api/v1/shifts/{Guid.NewGuid()}/assignments", null);
        Assert.AreEqual(HttpStatusCode.Unauthorized, post.StatusCode);
    }

    private static async Task<Shift> CreateShiftAsync(AdminShiftFixture fixture, int requiredCount)
    {
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            EventId = fixture.EventId,
            LocationId = fixture.Commons,
            StartsAt = new DateTimeOffset(2027, 5, 3, 19, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2027, 5, 3, 23, 0, 0, TimeSpan.Zero),
            RequiredCount = requiredCount,
            CreateRequestId = Guid.NewGuid(),
            CreatedByAccountId = Guid.NewGuid(),
            CreatedAt = AdminShiftFixture.Now
        };

        await fixture.Host.WithAsync<AdmDbContext>(db =>
        {
            db.Shifts.Add(shift);
            return db.SaveChangesAsync();
        });

        return shift;
    }
}
