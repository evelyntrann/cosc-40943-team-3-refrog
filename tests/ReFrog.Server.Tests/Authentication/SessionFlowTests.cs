using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class SessionFlowTests
{
    private static readonly System.Text.Json.JsonSerializerOptions WebJson =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task OrdinaryAccountSignsInForThirtyDaysAndUsesSafeDestination()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        await fixture.CreateAccountAsync("person@example.org", "Password1!");

        var response = await fixture.SignInAsync("person@example.org", "Password1!", "//outside.example");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SignInResponse>>();
        Assert.AreEqual("/account", body?.Data?.Destination);
        Assert.IsFalse(body?.Data?.IsAdministrator);
        Assert.AreEqual(fixture.Clock.GetUtcNow().AddDays(30), CookieExpiry(response));
        var cookie = SessionCookie(response);
        StringAssert.Contains(cookie, "secure", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(cookie, "httponly", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(cookie, "samesite=lax", StringComparison.OrdinalIgnoreCase);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AuthDbContext>()
            .AdministratorSignInAudits.CountAsync());
    }

    [TestMethod]
    public async Task UnknownWrongAndLockedCredentialsAreIndistinguishableUntilLockoutEnds()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        await fixture.CreateAccountAsync("person@example.org", "Password1!");

        var unknown = await fixture.SignInAsync("unknown@example.org", "Wrong1!");
        var wrong = await fixture.SignInAsync("person@example.org", "Wrong1!");
        for (var attempt = 2; attempt <= 5; attempt++)
        {
            await fixture.SignInAsync("person@example.org", "Wrong1!");
        }

        var locked = await fixture.SignInAsync("person@example.org", "Password1!");
        var unknownError = await ErrorBody(unknown);
        var wrongError = await ErrorBody(wrong);
        var lockedError = await ErrorBody(locked);
        Assert.AreEqual(unknownError, wrongError);
        Assert.AreEqual(wrongError, lockedError);
        Assert.AreEqual(HttpStatusCode.Unauthorized, locked.StatusCode);

        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var accepted = await fixture.SignInAsync("person@example.org", "Password1!");
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
    }

    [TestMethod]
    public async Task AdministratorAttemptsAreAuditedAndSuccessfulCookieUsesFourHours()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        await fixture.CreateAccountAsync("admin@tcu.edu", "Password1!", isAdministrator: true);

        var refused = await fixture.SignInAsync("admin@tcu.edu", "Wrong1!");
        var accepted = await fixture.SignInAsync("admin@tcu.edu", "Password1!");

        Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
        Assert.AreEqual(fixture.Clock.GetUtcNow().AddHours(4), CookieExpiry(accepted));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var audits = await scope.ServiceProvider.GetRequiredService<AuthDbContext>()
            .AdministratorSignInAudits.OrderBy(item => item.OccurredAt).ToListAsync();
        Assert.HasCount(2, audits);
        Assert.IsFalse(audits[0].Succeeded);
        Assert.IsTrue(audits[1].Succeeded);
    }

    [TestMethod]
    public async Task CurrentSessionAndAntiforgeryProtectedSignOutUseSharedEnvelope()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        await fixture.CreateAccountAsync("person@example.org", "Password1!");
        await fixture.SignInAsync("person@example.org", "Password1!");

        var current = await fixture.Client.GetFromJsonAsync<ApiResponse<CurrentSessionResponse>>(
            "/api/v1/me/session");
        Assert.AreEqual("person@example.org", current?.Data?.Email);

        using var rejected = await fixture.Client.DeleteAsync("/api/v1/auth/session");
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<ApiResponse<SignOutResponse>>();
        Assert.AreEqual("ANTIFORGERY_INVALID", rejectedBody?.Error?.Code);

        var token = await fixture.Client.GetFromJsonAsync<ApiResponse<AntiforgeryTokenResponse>>(
            "/api/v1/auth/antiforgery");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/session");
        request.Headers.Add("RequestVerificationToken", token!.Data!.Token);
        using var signedOut = await fixture.Client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, signedOut.StatusCode);

        using var after = await fixture.Client.GetAsync("/api/v1/me/session");
        Assert.AreEqual(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [TestMethod]
    public async Task MissingFieldsAreNamedWithoutEchoingCredentials()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/session",
            new SignInRequest(string.Empty, string.Empty));
        var raw = await response.Content.ReadAsStringAsync();
        var body = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<SignInResponse>>(raw, WebJson);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.IsTrue(body?.Error?.Fields?.ContainsKey("email"));
        Assert.IsTrue(body?.Error?.Fields?.ContainsKey("password"));
        Assert.DoesNotContain("Password1!", raw, StringComparison.Ordinal);
    }

    private static async Task<string> ErrorBody(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SignInResponse>>();
        return $"{body?.Error?.Code}|{body?.Error?.Message}";
    }

    private static DateTimeOffset CookieExpiry(HttpResponseMessage response)
    {
        var cookie = SessionCookie(response);
        var expires = cookie.Split(';', StringSplitOptions.TrimEntries)
            .Single(item => item.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..];
        return DateTimeOffset.Parse(expires, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string SessionCookie(HttpResponseMessage response)
    {
        Assert.IsTrue(response.Headers.TryGetValues("Set-Cookie", out var values));
        return values.Single(item =>
            item.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    private sealed class SessionFixture : IAsyncDisposable
    {
        private readonly string database;

        private SessionFixture(WebApplicationFactory<Program> factory, HttpClient client,
            ManualClock clock, string database)
        {
            Factory = factory;
            Client = client;
            Clock = clock;
            this.database = database;
        }

        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }
        public ManualClock Clock { get; }

        public static async Task<SessionFixture> CreateAsync()
        {
            var source = Environment.GetEnvironmentVariable("REFROG_TEST_SQL");
            if (string.IsNullOrWhiteSpace(source))
            {
                Assert.Inconclusive("Set REFROG_TEST_SQL to a disposable SQL Server test instance.");
            }

            var builder = new SqlConnectionStringBuilder(source)
            {
                InitialCatalog = $"ReFrogSessionTests_{Guid.NewGuid():N}",
            };
            var clock = new ManualClock(new DateTimeOffset(2026, 11, 13, 18, 0, 0, TimeSpan.Zero));
            var key = Convert.ToBase64String(new byte[32].Select((_, index) => (byte)(index + 1)).ToArray());
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Testing");
                host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ReFrog"] = builder.ConnectionString,
                        ["Authentication:VerificationKeys:1"] = key,
                        ["Authentication:ActiveVerificationKeyVersion"] = "1",
                    }));
                host.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                });
            });
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost/"),
                AllowAutoRedirect = false,
                HandleCookies = true,
            });
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
            return new(factory, client, clock, builder.InitialCatalog);
        }

        public async Task CreateAccountAsync(string email, string password, bool isAdministrator = false)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var account = new AuthAccount
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            Assert.IsTrue((await users.CreateAsync(account, password)).Succeeded);
            if (isAdministrator)
            {
                Assert.IsTrue((await roles.CreateAsync(new IdentityRole<Guid>("Administrator"))).Succeeded);
                Assert.IsTrue((await users.AddToRoleAsync(account, "Administrator")).Succeeded);
            }
        }

        public Task<HttpResponseMessage> SignInAsync(string email, string password, string? returnPath = null) =>
            Client.PostAsJsonAsync("/api/v1/auth/session", new SignInRequest(email, password, returnPath));

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await using var scope = Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureDeletedAsync();
            await Factory.DisposeAsync();
        }
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan elapsed) => now += elapsed;
    }
}
