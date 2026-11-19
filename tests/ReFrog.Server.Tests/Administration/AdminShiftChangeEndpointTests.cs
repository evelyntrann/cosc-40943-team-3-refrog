using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Administration;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Info.Persistence;
using ReFrog.Server.Features.Volunteer;

namespace ReFrog.Server.Tests.Administration;

// UC-ADM-manage-shifts 6a: ADM.md test rows "6a", "6a count change on a held shift", "6a shift with no held
// volunteers", "6a stale version", "6a write refusals", "Capacity reduction to held count", and "Capacity concurrent
// signup". Sample data only.
[TestClass]
public sealed class AdminShiftChangeEndpointTests
{
    private const string Admin = "admin@example.org";
    private const string Unresolved = "ASSIGNED_SHIFT_CHANGE_UNRESOLVED";

    [TestMethod]
    public async Task MovingOrRemovingAHeldShiftIsRefusedAndChangesNothing()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var moved = await PatchAsync(client, shift.Id, Edit(shift, date: "2027-05-05"), token);
        using var relocated = await PatchAsync(client, shift.Id, Edit(shift, location: fixture.Village), token);
        using var removed = await DeleteAsync(client, shift.Id, Version(shift), token);

        foreach (var response in new[] { moved, relocated, removed })
        {
            await AssertRefusedAsync(response, HttpStatusCode.Conflict, Unresolved);
        }

        await AssertUnchangedAsync(fixture, shift);
        Assert.HasCount(1, await fixture.AssignmentsAsync(shift.Id));
    }

    [TestMethod]
    public async Task ACountAtOrAboveHeldSavesWithOneAuditEntryAndLeavesVolunteersAlone()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var adminId = await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync(requiredCount: 3);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Pending);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var response = await PatchAsync(client, shift.Id, Edit(shift, count: 2), token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var saved = (await response.Content.ReadFromJsonAsync<ApiResponse<ManagedShift>>())!.Data!;
        Assert.AreEqual(2, saved.RequiredCount);
        Assert.AreEqual(2, saved.HeldCount);
        Assert.AreNotEqual(Version(shift), saved.Version);
        var stored = (await fixture.ShiftsAsync()).Single();
        Assert.AreEqual(2, stored.RequiredCount);
        Assert.AreEqual(shift.StartsAt, stored.StartsAt);
        Assert.AreEqual(Convert.ToHexString(stored.Version), saved.Version);
        var audit = (await fixture.AuditAsync()).Single();
        Assert.AreEqual("SHIFT_CHANGED", audit.Action);
        Assert.AreEqual(adminId, audit.ActorAccountId);
        Assert.AreEqual(shift.Id.ToString(), audit.TargetId);
        var assignments = await fixture.AssignmentsAsync(shift.Id);
        Assert.HasCount(2, assignments);
        CollectionAssert.AreEquivalent(
            new[] { AssignmentStatus.Active, AssignmentStatus.Pending }, assignments.Select(x => x.Status).ToArray());
    }

    [TestMethod]
    public async Task ACountBelowHeldIsRefusedAndNamesTheField()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync(requiredCount: 2);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Pending);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var response = await PatchAsync(client, shift.Id, Edit(shift, count: 1), token);

        var error = await AssertRefusedAsync(response, HttpStatusCode.Conflict, "CAPACITY_BELOW_HELD");
        Assert.IsNotNull(error.Fields?.GetValueOrDefault("requiredCount"));
        await AssertUnchangedAsync(fixture, shift);
    }

    [TestMethod]
    public async Task AShiftWithNoVolunteersCanBeMovedAndKeepsItsCountWhenNoneIsSent()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync(requiredCount: 3);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var response = await PatchAsync(client, shift.Id, Edit(shift, date: "2027-05-05", start: "15:00", end: "17:00"), token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var stored = (await fixture.ShiftsAsync()).Single();
        // 3:00–5:00 p.m. CDT on May 5, 2027 is 20:00–22:00 UTC.
        Assert.AreEqual(new DateTimeOffset(2027, 5, 5, 20, 0, 0, TimeSpan.Zero), stored.StartsAt);
        Assert.AreEqual(new DateTimeOffset(2027, 5, 5, 22, 0, 0, TimeSpan.Zero), stored.EndsAt);
        Assert.AreEqual(3, stored.RequiredCount);
    }

    [TestMethod]
    public async Task AnEditThatFailsTheCreateChecksNamesTheFieldAndChangesNothing()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        foreach (var (edit, field) in new[]
                 {
                     (Edit(shift, date: "2027-06-01"), "date"),
                     (Edit(shift, end: "21:00"), "endTime"),
                     (Edit(shift, location: fixture.ClosedPod), "locationId"),
                     (Edit(shift, count: 0), "requiredCount"),
                 })
        {
            using var response = await PatchAsync(client, shift.Id, edit, token);

            var error = await AssertRefusedAsync(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED");
            Assert.IsNotNull(error.Fields?.GetValueOrDefault(field), field);
        }

        await AssertUnchangedAsync(fixture, shift);
    }

    [TestMethod]
    public async Task ACountChangeIsNotBlockedByALocationThatHasSinceClosed()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync(fixture.ClosedPod);
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var response = await PatchAsync(client, shift.Id, Edit(shift, count: 5), token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.AreEqual(5, (await fixture.ShiftsAsync()).Single().RequiredCount);
    }

    [TestMethod]
    public async Task AShiftWithNoAssignmentsIsRemovedWithOneAuditEntry()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        var adminId = await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var response = await DeleteAsync(client, shift.Id, Version(shift), token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.AreEqual(shift.Id, (await response.Content.ReadFromJsonAsync<ApiResponse<ShiftRemovedResponse>>())!.Data!.ShiftId);
        Assert.IsEmpty(await fixture.ShiftsAsync());
        var audit = (await fixture.AuditAsync()).Single();
        Assert.AreEqual("SHIFT_REMOVED", audit.Action);
        Assert.AreEqual(adminId, audit.ActorAccountId);
    }

    [TestMethod]
    public async Task RemovalDoesNotDependOnReadingLocations()
    {
        var failure = new FailLocationReads();
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
            services.ConfigureDbContext<InfoDbContext>((_, options) => options.AddInterceptors(failure)));
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        failure.Armed = true;
        using var response = await DeleteAsync(client, shift.Id, Version(shift), token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.IsEmpty(await fixture.ShiftsAsync());
    }

    [TestMethod]
    public async Task RemovalIsRefusedWhileCancelledOrDeniedAssignmentsAreOnRecord()
    {
        foreach (var status in new[] { AssignmentStatus.Cancelled, AssignmentStatus.Denied })
        {
            await using var fixture = await AdminShiftFixture.CreateAsync();
            await fixture.CreateAccountAsync(Admin, isAdministrator: true);
            var shift = await fixture.SeedShiftAsync();
            await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), status);
            using var client = await fixture.SignedInClientAsync(Admin);
            var token = await AdminShiftFixture.TokenAsync(client);

            using var response = await DeleteAsync(client, shift.Id, Version(shift), token);

            await AssertRefusedAsync(response, HttpStatusCode.Conflict, Unresolved);
            await AssertUnchangedAsync(fixture, shift);
        }
    }

    [TestMethod]
    public async Task AnOutOfDateVersionIsRefusedAndABadOneNamesTheField()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        const string stale = "00000000000007D0";

        using var patchStale = await PatchAsync(client, shift.Id, Edit(shift, count: 5) with { ExpectedVersion = stale }, token);
        using var deleteStale = await DeleteAsync(client, shift.Id, stale, token);
        using var patchMissing = await PatchAsync(client, shift.Id, Edit(shift, count: 5) with { ExpectedVersion = null }, token);
        using var deleteMissing = await DeleteAsync(client, shift.Id, null, token);
        using var deleteMalformed = await DeleteAsync(client, shift.Id, "not-a-version", token);

        await AssertRefusedAsync(patchStale, HttpStatusCode.Conflict, "SHIFT_VERSION_STALE");
        await AssertRefusedAsync(deleteStale, HttpStatusCode.Conflict, "SHIFT_VERSION_STALE");
        foreach (var response in new[] { patchMissing, deleteMissing, deleteMalformed })
        {
            var error = await AssertRefusedAsync(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED");
            Assert.IsNotNull(error.Fields?.GetValueOrDefault("expectedVersion"));
        }

        await AssertUnchangedAsync(fixture, shift);
    }

    [TestMethod]
    public async Task ASecondSaveFromTheSamePreviewIsRefusedAsStale()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var first = await PatchAsync(client, shift.Id, Edit(shift, count: 4), token);
        using var second = await PatchAsync(client, shift.Id, Edit(shift, count: 6), token);

        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        await AssertRefusedAsync(second, HttpStatusCode.Conflict, "SHIFT_VERSION_STALE");
        Assert.AreEqual(4, (await fixture.ShiftsAsync()).Single().RequiredCount);
    }

    [TestMethod]
    public async Task SignedOutNonAdministratorAndMissingTokenWritesAreRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.CreateAccountAsync("volunteer@example.org", isAdministrator: false);
        var shift = await fixture.SeedShiftAsync();
        using var anonymous = fixture.Host.CreateClient();
        using var volunteer = await fixture.SignedInClientAsync("volunteer@example.org");
        using var admin = await fixture.SignedInClientAsync(Admin);
        var anonymousToken = await AdminShiftFixture.TokenAsync(anonymous);
        var volunteerToken = await AdminShiftFixture.TokenAsync(volunteer);

        foreach (var (client, token, status, code) in new[]
                 {
                     (anonymous, anonymousToken, HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"),
                     (volunteer, volunteerToken, HttpStatusCode.Forbidden, "ADMINISTRATOR_REQUIRED"),
                     (admin, (string?)null, HttpStatusCode.BadRequest, "ANTIFORGERY_INVALID"),
                 })
        {
            using var patch = await PatchAsync(client, shift.Id, Edit(shift, count: 5), token);
            using var delete = await DeleteAsync(client, shift.Id, Version(shift), token);

            await AssertRefusedAsync(patch, status, code);
            await AssertRefusedAsync(delete, status, code);
        }

        await AssertUnchangedAsync(fixture, shift);
    }

    [TestMethod]
    public async Task UnknownShiftsAndShiftsOutsideTheCurrentEventAreNotFound()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var other = await fixture.SeedShiftAsync(eventId: Guid.NewGuid());
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        using var unknownPatch = await PatchAsync(client, Guid.NewGuid(), Edit(other, count: 5), token);
        using var unknownDelete = await DeleteAsync(client, Guid.NewGuid(), Version(other), token);
        using var otherPatch = await PatchAsync(client, other.Id, Edit(other, count: 5), token);
        using var otherDelete = await DeleteAsync(client, other.Id, Version(other), token);

        foreach (var response in new[] { unknownPatch, unknownDelete, otherPatch, otherDelete })
        {
            await AssertRefusedAsync(response, HttpStatusCode.NotFound, "NOT_FOUND");
        }

        await AssertUnchangedAsync(fixture, other);
    }

    [TestMethod]
    public async Task AStoreFailureIsNotRecordedChangesNothingAndCanBeRetried()
    {
        var failure = new FailNextSave();
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
            services.ConfigureDbContext<AdmDbContext>((_, options) => options.AddInterceptors(failure)));
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);

        failure.Armed = true;
        using var patch = await PatchAsync(client, shift.Id, Edit(shift, count: 5), token);
        failure.Armed = true;
        using var delete = await DeleteAsync(client, shift.Id, Version(shift), token);

        await AssertRefusedAsync(patch, HttpStatusCode.ServiceUnavailable, "NOT_RECORDED");
        await AssertRefusedAsync(delete, HttpStatusCode.ServiceUnavailable, "NOT_RECORDED");
        await AssertUnchangedAsync(fixture, shift);

        using var retry = await PatchAsync(client, shift.Id, Edit(shift, count: 5), token);
        Assert.AreEqual(HttpStatusCode.OK, retry.StatusCode, await retry.Content.ReadAsStringAsync());
        Assert.HasCount(1, await fixture.AuditAsync());
    }

    [TestMethod]
    public async Task ABodyThatIsNotJsonIsRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);
        var token = await AdminShiftFixture.TokenAsync(client);
        using var message = new HttpRequestMessage(HttpMethod.Patch, ShiftUri(shift.Id))
        {
            Content = new StringContent("not json"),
        };
        message.Headers.Add("RequestVerificationToken", token);

        using var response = await client.SendAsync(message);

        await AssertRefusedAsync(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED");
        await AssertUnchangedAsync(fixture, shift);
    }

    [TestMethod]
    public async Task AConcurrentCapacityCutAndSignUpNeverOverbookTheShift()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        for (var round = 0; round < 6; round++)
        {
            var shift = await fixture.SeedShiftAsync(requiredCount: 2);
            await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Active);

            var signUp = SignUpAsync(fixture, shift.Id);
            var cut = CutCapacityAsync(fixture, shift);
            await Task.WhenAll(signUp, cut);

            var stored = (await fixture.ShiftsAsync()).Single(x => x.Id == shift.Id);
            var held = (await fixture.AssignmentsAsync(shift.Id)).Count(x => x.Status == AssignmentStatus.Active);
            Assert.IsLessThanOrEqualTo(stored.RequiredCount, held, $"Round {round} overbooked the shift.");
            if (cut.Result == 200)
            {
                Assert.AreEqual(1, stored.RequiredCount);
                Assert.AreEqual(409, signUp.Result, "With the count cut first, the sign-up must find the shift full.");
            }
            else
            {
                Assert.AreEqual(409, cut.Result);
                Assert.AreEqual(201, signUp.Result, "With the sign-up first, the cut must be refused.");
                Assert.AreEqual(2, stored.RequiredCount);
            }
        }
    }

    private static async Task<int> SignUpAsync(AdminShiftFixture fixture, Guid shiftId)
    {
        await using var scope = fixture.Host.Factory.Services.CreateAsyncScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<AssignmentService>()
            .SignUpAsync(Guid.NewGuid(), holdsVolunteerRole: true, shiftId, CancellationToken.None);
        return outcome.StatusCode;
    }

    private static async Task<int> CutCapacityAsync(AdminShiftFixture fixture, Shift shift)
    {
        await using var scope = fixture.Host.Factory.Services.CreateAsyncScope();
        var reply = await scope.ServiceProvider.GetRequiredService<ShiftAdminService>()
            .ApplyConfirmedChangeAsync(shift.Id, Edit(shift, count: 1), Guid.NewGuid(), CancellationToken.None);
        return reply.StatusCode;
    }

    private static string Version(Shift shift) => Convert.ToHexString(shift.Version);

    /// <summary>The seeded shift's own details (Monday May 3, 2:00–6:00 p.m.), with the given ones changed.</summary>
    private static ChangeShiftRequest Edit(
        Shift shift, string date = "2027-05-03", string start = "14:00", string end = "18:00", int? count = null, Guid? location = null) =>
        new(Version(shift), location ?? shift.LocationId, date, start, end, count);

    private static Uri ShiftUri(Guid shiftId, string? version = null) => new(
        $"/api/v1/admin/shifts/{shiftId}" + (version is null ? string.Empty : $"?expectedVersion={version}"), UriKind.Relative);

    private static async Task<HttpResponseMessage> PatchAsync(HttpClient client, Guid shiftId, ChangeShiftRequest body, string? token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, ShiftUri(shiftId)) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            message.Headers.Add("RequestVerificationToken", token);
        }

        return await client.SendAsync(message);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid shiftId, string? version, string? token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, ShiftUri(shiftId, version));
        if (token is not null)
        {
            message.Headers.Add("RequestVerificationToken", token);
        }

        return await client.SendAsync(message);
    }

    private static async Task<ApiError> AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.AreEqual(status, response.StatusCode, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.AreEqual(code, body?.Error?.Code);
        Assert.IsNull(body?.Data);
        return body!.Error!;
    }

    private static async Task AssertUnchangedAsync(AdminShiftFixture fixture, Shift shift)
    {
        var stored = (await fixture.ShiftsAsync()).Single(x => x.Id == shift.Id);
        Assert.AreEqual(shift.RequiredCount, stored.RequiredCount);
        Assert.AreEqual(shift.LocationId, stored.LocationId);
        Assert.AreEqual(shift.StartsAt, stored.StartsAt);
        Assert.AreEqual(shift.EndsAt, stored.EndsAt);
        CollectionAssert.AreEqual(shift.Version, stored.Version);
        Assert.IsEmpty(await fixture.AuditAsync());
    }

    /// <summary>Fails every read of INF's locations, as an outage of that table would.</summary>
    private sealed class FailLocationReads : DbCommandInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("[DonationLocations]", StringComparison.Ordinal))
            {
                throw new TimeoutException("Simulated timeout.");
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Fails the next save on ADM's store, as a dropped connection would.</summary>
    private sealed class FailNextSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Armed)
            {
                return ValueTask.FromResult(result);
            }

            Armed = false;
            throw new DbUpdateException("Simulated failure.");
        }
    }
}
