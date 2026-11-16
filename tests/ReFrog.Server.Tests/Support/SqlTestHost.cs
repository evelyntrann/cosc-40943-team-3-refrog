using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Server.Features.Administration.Persistence;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.Info.Persistence;

namespace ReFrog.Server.Tests.Support;

/// <summary>
/// The app against its own disposable SQL Server database, with every area's migrations applied and a fixed clock
/// (architecture §8.2.2, §8.2.10). The database is dropped on dispose. Requires <c>REFROG_TEST_SQL</c>.
/// </summary>
internal sealed class SqlTestHost : IAsyncDisposable
{
    private SqlTestHost(WebApplicationFactory<Program> factory, ManualClock clock, string connectionString)
    {
        Factory = factory;
        Clock = clock;
        ConnectionString = connectionString;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public ManualClock Clock { get; }

    public string ConnectionString { get; }

    public static async Task<SqlTestHost> CreateAsync(
        DateTimeOffset now, Action<IServiceCollection>? configureServices = null)
    {
        var source = Environment.GetEnvironmentVariable("REFROG_TEST_SQL");
        if (string.IsNullOrWhiteSpace(source))
        {
            Assert.Inconclusive("Set REFROG_TEST_SQL to a disposable SQL Server test instance.");
        }

        var builder = new SqlConnectionStringBuilder(source) { InitialCatalog = $"ReFrogTest_{Guid.NewGuid():N}" };
        var clock = new ManualClock(now);
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(index => (byte)index).ToArray());
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReFrog"] = builder.ConnectionString,
                ["Authentication:VerificationKeys:1"] = key,
                ["Authentication:ActiveVerificationKeyVersion"] = "1",
            }));
            host.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                configureServices?.Invoke(services);
            });
        });
        var host = new SqlTestHost(factory, clock, builder.ConnectionString);
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            foreach (var context in MigratedContexts(scope.ServiceProvider))
            {
                await context.Database.MigrateAsync();
            }

            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost/"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public async Task WithAsync<TContext>(Func<TContext, Task> action)
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TContext>());
    }

    public async Task<T> WithAsync<TContext, T>(Func<TContext, Task<T>> action)
        where TContext : DbContext
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TContext>());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(
                new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString);
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            drop.CommandText = $"IF DB_ID(N'{database}') IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END";
            await drop.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Keep the test's own result as the reported outcome.
        }

        await Factory.DisposeAsync();
    }

    private static IEnumerable<DbContext> MigratedContexts(IServiceProvider services)
    {
        yield return services.GetRequiredService<AuthDbContext>();
        yield return services.GetRequiredService<InfoDbContext>();
        yield return services.GetRequiredService<AdmDbContext>();
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan elapsed) => now += elapsed;
}
