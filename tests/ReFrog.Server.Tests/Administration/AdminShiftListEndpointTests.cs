using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Volunteer;
using ReFrog.Server.Features.Volunteer;

namespace ReFrog.Server.Tests.Administration;

// UC-ADM-manage-shifts 6a: ADM.md test row "6a shift list". Sample data only.
[TestClass]
public sealed class AdminShiftListEndpointTests
{
    private const string Admin = "admin@example.org";
    private const string Volunteer = "volunteer@example.org";
    private static readonly Uri ListUri = new("/api/v1/admin/shifts", UriKind.Relative);

    [TestMethod]
    public async Task AdministratorSeesTheCurrentEventsShiftsWithHeldCounts()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync();
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        var shift = await fixture.SeedShiftAsync(requiredCount: 4);
        var empty = await fixture.SeedShiftAsync(fixture.Village);
        await fixture.SeedShiftAsync(eventId: Guid.NewGuid());
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Active);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Pending);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Cancelled);
        await fixture.SeedAssignmentAsync(shift.Id, Guid.NewGuid(), AssignmentStatus.Denied);
        using var client = await fixture.SignedInClientAsync(Admin);

        var shifts = (await client.GetFromJsonAsync<ApiResponse<ShiftListResponse>>(ListUri))!.Data!.Shifts;

        Assert.HasCount(2, shifts);
        var listed = shifts.Single(x => x.ShiftId == shift.Id);
        Assert.AreEqual("Commons", listed.LocationName);
        Assert.AreEqual(4, listed.RequiredCount);
        Assert.AreEqual(2, listed.HeldCount, "Cancelled and denied sign-ups hold no slot.");
        Assert.AreEqual(shift.StartsAt, listed.StartsAt);
        Assert.AreEqual(Convert.ToHexString(shift.Version), listed.Version);
        Assert.AreEqual(0, shifts.Single(x => x.ShiftId == empty.Id).HeldCount);
    }

    [TestMethod]
    public async Task SignedOutAndNonAdministratorCallersGetNoShifts()
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
            using var response = await client.GetAsync(ListUri);

            Assert.AreEqual(status, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftListResponse>>();
            Assert.AreEqual(code, body?.Error?.Code);
            Assert.IsNull(body?.Data);
        }
    }

    [TestMethod]
    public async Task NoCurrentEventIsRefused()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(withEvent: false);
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        using var client = await fixture.SignedInClientAsync(Admin);

        using var response = await client.GetAsync(ListUri);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftListResponse>>();
        Assert.AreEqual("NO_CURRENT_EVENT", body?.Error?.Code);
    }

    [TestMethod]
    public async Task UnreadableAssignmentsAreUnavailableNotAZeroCount()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(services =>
        {
            services.RemoveAll<IAssignmentQueryService>();
            services.AddScoped<IAssignmentQueryService, UnreadableAssignments>();
        });
        await fixture.CreateAccountAsync(Admin, isAdministrator: true);
        await fixture.SeedShiftAsync();
        using var client = await fixture.SignedInClientAsync(Admin);

        using var response = await client.GetAsync(ListUri);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ShiftListResponse>>();
        Assert.AreEqual("UNAVAILABLE", body?.Error?.Code);
        Assert.IsNull(body?.Data);
    }
}

/// <summary>VOL's read contract when the store cannot answer.</summary>
internal sealed class UnreadableAssignments : IAssignmentQueryService
{
    public Task<int> CountHeldAsync(Guid shiftId, CancellationToken cancellationToken) => throw Failure();

    public Task<bool> IsActiveAsync(Guid shiftId, Guid volunteerId, CancellationToken cancellationToken) => throw Failure();

    public Task<IReadOnlyDictionary<Guid, int>> CountsHeldByShiftsAsync(
        IReadOnlyCollection<Guid> shiftIds, CancellationToken cancellationToken) => throw Failure();

    public Task<IReadOnlyDictionary<Guid, AssignmentCounts>> CountsByShiftsAsync(
        IReadOnlyCollection<Guid> shiftIds, CancellationToken cancellationToken) => throw Failure();

    public Task<IReadOnlyList<HeldAssignment>> ListHeldAsync(Guid shiftId, CancellationToken cancellationToken) => throw Failure();

    public Task<bool> HasAnyAssignmentAsync(Guid shiftId, CancellationToken cancellationToken) => throw Failure();

    private static TimeoutException Failure() => new("Simulated timeout.");
}
