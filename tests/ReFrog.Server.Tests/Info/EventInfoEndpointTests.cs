using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Info;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Info.Persistence;

namespace ReFrog.Server.Tests.Info;

// UC-INF-view-event-info against SQL Server (docs/design/INF.md test rows "main", "2a",
// and "Unauthenticated access"). Event values are sample data, not real event dates.
[TestClass]
public sealed class EventInfoEndpointTests
{
    private const string Route = "/api/v1/info/event";

    [TestMethod]
    public async Task AnonymousCallerGetsTheActiveEvent()
    {
        await using var fixture = await InfoFixture.CreateAsync();
        await fixture.AddAsync(Event(ReFrogEventStatus.Closed, 2026), Event(ReFrogEventStatus.Active, 2027));

        using var response = await fixture.Client.GetAsync(new Uri(Route, UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<EventInfoResponse>>();
        Assert.IsNull(body?.Error);
        Assert.AreEqual(EventInfoStatus.Active, body?.Data?.Status);
        Assert.AreEqual(2027, body?.Data?.EventYear);
        Assert.AreEqual(new EventScheduleWindow(new DateOnly(2027, 5, 3), new DateOnly(2027, 5, 8), "Sample hours 2027"),
            body?.Data?.ScheduleWindow);
        Assert.AreEqual("Sample overview 2027", body?.Data?.CampusOverview);
    }

    [TestMethod]
    public async Task NoEventReturnsOffSeason()
    {
        await using var fixture = await InfoFixture.CreateAsync();

        var body = await fixture.Client.GetFromJsonAsync<ApiResponse<EventInfoResponse>>(new Uri(Route, UriKind.Relative));

        Assert.AreEqual(EventInfoStatus.OffSeason, body?.Data?.Status);
        Assert.IsNull(body?.Data?.ScheduleWindow);
    }

    [TestMethod]
    public async Task PlannedClosedAndArchivedEventsAreNotShownAsActive()
    {
        await using var fixture = await InfoFixture.CreateAsync();
        await fixture.AddAsync(Event(ReFrogEventStatus.Archived, 2025), Event(ReFrogEventStatus.Closed, 2026),
            Event(ReFrogEventStatus.Planned, 2027));

        var body = await fixture.Client.GetFromJsonAsync<ApiResponse<EventInfoResponse>>(new Uri(Route, UriKind.Relative));

        Assert.AreEqual(EventInfoStatus.OffSeason, body?.Data?.Status);
        Assert.IsNull(body?.Data?.EventYear);
    }

    [TestMethod]
    public async Task SecondActiveEventIsRejected()
    {
        await using var fixture = await InfoFixture.CreateAsync();
        await fixture.AddAsync(Event(ReFrogEventStatus.Active, 2027));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => fixture.AddAsync(Event(ReFrogEventStatus.Active, 2028)));
    }

    [TestMethod]
    public async Task EventEndingBeforeItStartsIsRejected()
    {
        await using var fixture = await InfoFixture.CreateAsync();
        var backwards = Event(ReFrogEventStatus.Planned, 2027);
        backwards.EndDate = backwards.StartDate.AddDays(-1);

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => fixture.AddAsync(backwards));
    }

    [TestMethod]
    public async Task StoreFailureReturnsTheSharedUnexpectedError()
    {
        // No database needed: the store itself fails.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
            {
                services.RemoveAll<IEventInfoStore>();
                services.AddScoped<IEventInfoStore, FailingStore>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/") });

        using var response = await client.GetAsync(new Uri(Route, UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<EventInfoResponse>>();
        Assert.IsNull(body?.Data);
        Assert.AreEqual(ApiErrorCategory.Unexpected, body?.Error?.Category);
        Assert.AreEqual("UNEXPECTED_ERROR", body?.Error?.Code);
    }

    [TestMethod]
    public async Task EventInfoStaysPublicWhenSignInIsRequiredByDefault()
    {
        // If the app ever requires sign-in by default, the event endpoint must still be public
        // (architecture §8.1; INF.md "Unauthenticated access"). No database needed.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
            {
                services.RemoveAll<IEventInfoStore>();
                services.AddScoped<IEventInfoStore, NoEventStore>();
                services.AddAuthorizationBuilder().SetFallbackPolicy(
                    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false,
        });

        using var protectedRoute = await client.GetAsync(new Uri("/api/v1/not-a-route", UriKind.Relative));
        using var eventInfo = await client.GetAsync(new Uri(Route, UriKind.Relative));

        // Control: the default policy is in force, so a route without AllowAnonymous is refused.
        Assert.AreNotEqual(HttpStatusCode.NotFound, protectedRoute.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.OK, protectedRoute.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, eventInfo.StatusCode);
    }

    private static ReFrogEvent Event(string status, int year) => new()
    {
        Id = Guid.NewGuid(),
        EventYear = year,
        StartDate = new DateOnly(year, 5, 3),
        EndDate = new DateOnly(year, 5, 8),
        DailyOperatingHoursDescription = $"Sample hours {year}",
        CampusOverview = $"Sample overview {year}",
        Status = status,
    };

    private sealed class NoEventStore : IEventInfoStore
    {
        public Task<EventInfo?> FindActiveEventAsync(CancellationToken cancellationToken) => Task.FromResult<EventInfo?>(null);
    }

    private sealed class FailingStore : IEventInfoStore
    {
        public Task<EventInfo?> FindActiveEventAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated store failure.");
    }

    private sealed class InfoFixture : IAsyncDisposable
    {
        private InfoFixture(WebApplicationFactory<Program> factory, HttpClient client)
        {
            Factory = factory;
            Client = client;
        }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client { get; }

        public async Task AddAsync(params ReFrogEvent[] events)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<InfoDbContext>();
            db.Events.AddRange(events);
            await db.SaveChangesAsync();
        }

        public static async Task<InfoFixture> CreateAsync()
        {
            var source = Environment.GetEnvironmentVariable("REFROG_TEST_SQL");
            if (string.IsNullOrWhiteSpace(source))
            {
                Assert.Inconclusive("Set REFROG_TEST_SQL to a disposable SQL Server test instance.");
            }

            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(source)
            {
                InitialCatalog = $"ReFrogInfoTest_{Guid.NewGuid():N}",
            };
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
                host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ConnectionStrings:ReFrog"] = builder.ConnectionString })));
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/") });
            var fixture = new InfoFixture(factory, client);
            try
            {
                await using var scope = factory.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<InfoDbContext>().Database.MigrateAsync();
                return fixture;
            }
            catch
            {
                // Drop a half-created database and release the host so a failed setup leaves nothing behind.
                try
                {
                    await fixture.DisposeAsync();
                }
                catch (Microsoft.Data.SqlClient.SqlException)
                {
                    // Keep the original setup failure as the reported error.
                }

                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InfoDbContext>().Database.EnsureDeletedAsync();
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }
}
