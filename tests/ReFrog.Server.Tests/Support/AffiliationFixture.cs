using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Tests.Support;

/// <summary>
/// The app against a disposable SQL Server with a fixed clock, a capturing email adapter, and captured logs,
/// for TCU affiliation verification (IDV.md "Reuse &amp; cross-cutting", §8.2.10). Synthetic accounts only.
/// </summary>
internal sealed partial class AffiliationFixture : IAsyncDisposable
{
    public const string Password = "Password1!";
    public static readonly DateTimeOffset Now = new(2027, 4, 26, 15, 0, 0, TimeSpan.Zero);

    private AffiliationFixture(SqlTestHost host, CaptureEmailSender email, CapturingLoggerProvider logs)
    {
        Host = host;
        Email = email;
        Logs = logs;
    }

    public SqlTestHost Host { get; }

    public CaptureEmailSender Email { get; }

    public CapturingLoggerProvider Logs { get; }

    public ManualClock Clock => Host.Clock;

    public static async Task<AffiliationFixture> CreateAsync(Action<IServiceCollection>? configureServices = null)
    {
        var email = new CaptureEmailSender();
        var logs = new CapturingLoggerProvider();
        var host = await SqlTestHost.CreateAsync(Now, services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(email);
            services.AddSingleton<ILoggerProvider>(logs);
            configureServices?.Invoke(services);
        });
        return new AffiliationFixture(host, email, logs);
    }

    public async Task<Guid> CreateAccountAsync(string email, params string[] roles)
    {
        await using var scope = Host.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
        var roleStore = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var account = new AuthAccount { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true };
        Assert.IsTrue((await users.CreateAsync(account, Password)).Succeeded);
        foreach (var role in roles)
        {
            if (!await roleStore.RoleExistsAsync(role))
            {
                Assert.IsTrue((await roleStore.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            }

            Assert.IsTrue((await users.AddToRoleAsync(account, role)).Succeeded);
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

    public async Task<T> WithServicesAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Host.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task<T> WithAuthAsync<T>(Func<AuthDbContext, Task<T>> action) => Host.WithAsync(action);

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    [GeneratedRegex(@"\b\d{6}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    internal sealed class CaptureEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> sent = new();

        public EmailDeliveryOutcome Outcome { get; set; } = EmailDeliveryOutcome.Accepted;

        public bool ThrowOnSend { get; set; }

        /// <summary>When set, each send waits on it, so a test can act while the send is in flight.</summary>
        public TaskCompletionSource? Gate { get; set; }

        public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Count => sent.Count;

        public IReadOnlyList<EmailMessage> Sent => [.. sent];

        public string LastCode => sent.IsEmpty ? string.Empty : CodePattern().Match(sent.Last().PlainTextBody).Value;

        public async Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            sent.Enqueue(message);
            Entered.TrySetResult();
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            if (ThrowOnSend)
            {
                throw new InvalidOperationException($"provider-sensitive-detail {message.Destination} {message.PlainTextBody}");
            }

            return Outcome;
        }

        public void ResetEntered() => Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Text)> entries = new();

        public IEnumerable<string> Messages => entries.Select(entry => entry.Text);

        public IEnumerable<(LogLevel Level, string Text)> Entries => entries;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue((logLevel, $"{eventId.Name} {formatter(state, exception)} {exception}"));
        }
    }
}
