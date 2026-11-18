using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Contracts.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.Info.Persistence;
using ReFrog.Server.Tests.Info;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Administration;

/// <summary>
/// The app with a sample planned event (Monday May 3 to Saturday May 8, 2027) and three locations:
/// Commons (open Monday and Wednesday 2:00–8:00 p.m.), Village (open Monday 2:00–6:00 p.m., no default count),
/// and Closed Pod (closed). Sample data only (§8.2.10).
/// </summary>
internal sealed class AdminShiftFixture : IAsyncDisposable
{
    public const string Password = "Password1!";
    public static readonly DateTimeOffset Now = new(2027, 3, 1, 15, 0, 0, TimeSpan.Zero);

    private AdminShiftFixture(SqlTestHost host) => Host = host;

    public SqlTestHost Host { get; }
    public Guid EventId { get; private set; }
    public Guid Commons { get; private set; }
    public Guid Village { get; private set; }
    public Guid ClosedPod { get; private set; }

    public static async Task<AdminShiftFixture> CreateAsync(
        Action<IServiceCollection>? configureServices = null, bool withEvent = true)
    {
        var fixture = new AdminShiftFixture(await SqlTestHost.CreateAsync(Now, configureServices));
        if (withEvent)
        {
            await fixture.SeedEventAsync();
        }

        return fixture;
    }

    public async Task<Guid> CreateAccountAsync(string email, bool isAdministrator)
    {
        await using var scope = Host.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var account = new AuthAccount { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true };
        Assert.IsTrue((await users.CreateAsync(account, Password)).Succeeded);
        if (isAdministrator)
        {
            if (!await roles.RoleExistsAsync(SessionService.AdministratorRole))
            {
                Assert.IsTrue((await roles.CreateAsync(new IdentityRole<Guid>(SessionService.AdministratorRole))).Succeeded);
            }

            Assert.IsTrue((await users.AddToRoleAsync(account, SessionService.AdministratorRole)).Succeeded);
        }

        return account.Id;
    }

    public async Task<HttpClient> SignedInClientAsync(string email)
    {
        var client = Host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/session", new SignInRequest(email, Password));
        Assert.IsTrue(response.IsSuccessStatusCode, $"Sign-in failed: {(int)response.StatusCode}");
        return client;
    }

    public static async Task<string> TokenAsync(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<ApiResponse<AntiforgeryTokenResponse>>("/api/v1/auth/antiforgery");
        return token!.Data!.Token;
    }

    public static async Task<HttpResponseMessage> PostShiftAsync(HttpClient client, CreateShiftRequest request, string? token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/shifts") { Content = JsonContent.Create(request) };
        if (token is not null)
        {
            message.Headers.Add("RequestVerificationToken", token);
        }

        return await client.SendAsync(message);
    }

    public static async Task<HttpResponseMessage> PutStaffingAsync(
        HttpClient client, Guid eventId, Guid locationId, int? count, string? token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v1/admin/events/{eventId}/locations/{locationId}/staffing")
        {
            Content = JsonContent.Create(new SetLocationStaffingRequest(count)),
        };
        if (token is not null)
        {
            message.Headers.Add("RequestVerificationToken", token);
        }

        return await client.SendAsync(message);
    }

    /// <summary>A valid request at Commons on Monday May 3, 2:00–6:00 p.m., using the location default.</summary>
    public CreateShiftRequest ValidRequest(int? requiredCount = null) =>
        new(Guid.NewGuid(), Commons, "2027-05-03", "14:00", "18:00", requiredCount);

    public Task SetDefaultDirectlyAsync(Guid locationId, int count) => Host.WithAsync<AdmDbContext>(db =>
    {
        db.LocationStaffing.Add(new LocationStaffing { EventId = EventId, LocationId = locationId, DefaultRequiredCount = count });
        return db.SaveChangesAsync();
    });

    public Task<List<Shift>> ShiftsAsync() => Host.WithAsync<AdmDbContext, List<Shift>>(db => db.Shifts.AsNoTracking().ToListAsync());

    public Task<List<AdminAuditEntry>> AuditAsync() =>
        Host.WithAsync<AdmDbContext, List<AdminAuditEntry>>(db => db.AuditEntries.AsNoTracking().ToListAsync());

    public Task<int> StaffingCountAsync() => Host.WithAsync<AdmDbContext, int>(db => db.LocationStaffing.CountAsync());

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    private async Task SeedEventAsync()
    {
        var planned = EventConfigServiceTests.Event(ReFrogEventStatus.Planned, 2027);
        var commons = EventConfigServiceTests.Location(planned.Id, "Commons", DonationLocationStatus.Open);
        commons.Schedules.Add(EventConfigServiceTests.Hours("MONDAY", 14, 20));
        commons.Schedules.Add(EventConfigServiceTests.Hours("WEDNESDAY", 14, 20));
        var village = EventConfigServiceTests.Location(planned.Id, "Village", DonationLocationStatus.Open);
        village.Schedules.Add(EventConfigServiceTests.Hours("MONDAY", 14, 18));
        var closed = EventConfigServiceTests.Location(planned.Id, "Closed Pod", DonationLocationStatus.Closed);
        closed.Schedules.Add(EventConfigServiceTests.Hours("MONDAY", 14, 18));
        await Host.WithAsync<InfoDbContext>(db =>
        {
            db.Events.Add(planned);
            db.Locations.AddRange(commons, village, closed);
            return db.SaveChangesAsync();
        });
        (EventId, Commons, Village, ClosedPod) = (planned.Id, commons.Id, village.Id, closed.Id);
    }
}
