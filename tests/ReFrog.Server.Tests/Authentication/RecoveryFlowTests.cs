using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class RecoveryFlowTests
{
    [TestMethod]
    public async Task RecoveryResetsPasswordAndInvalidatesEveryEarlierSession()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var firstSession = await fixture.SignInAsync();
        using var secondSession = await fixture.SignInAsync();
        var acknowledgement = await fixture.RequestAsync("PERSON@EXAMPLE.ORG");
        Assert.AreEqual(HttpStatusCode.Accepted, acknowledgement.StatusCode);
        Assert.HasCount(1, fixture.Email.Messages);
        var message = fixture.Email.Messages.Single();
        Assert.AreEqual("Person@example.org", message.Destination);
        Assert.AreEqual("refrog.example", fixture.ResetLink.Host);
        Assert.AreEqual(string.Empty, fixture.ResetLink.Query);
        Assert.AreEqual("/reset-password", fixture.ResetLink.AbsolutePath);

        var reset = await fixture.ResetAsync();
        Assert.AreEqual(HttpStatusCode.OK, reset.StatusCode);
        Assert.IsTrue((await reset.Content.ReadFromJsonAsync<ApiResponse<PasswordResetResponse>>())?.Data?.PasswordChanged);
        Assert.IsFalse(reset.Headers.Contains("Set-Cookie"));
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await firstSession.GetAsync("/api/v1/me/session")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await secondSession.GetAsync("/api/v1/me/session")).StatusCode);
        await fixture.AssertPasswordAsync("NewPassword2!", "Password1!");

        using var oldCredentials = await fixture.Client.PostAsJsonAsync("/api/v1/auth/session",
            new SignInRequest("Person@example.org", "Password1!"));
        Assert.AreEqual(HttpStatusCode.Unauthorized, oldCredentials.StatusCode);
        using var newCredentials = await fixture.Client.PostAsJsonAsync("/api/v1/auth/session",
            new SignInRequest("Person@example.org", "NewPassword2!"));
        Assert.AreEqual(HttpStatusCode.OK, newCredentials.StatusCode);
    }

    [TestMethod]
    public async Task KnownUnknownAndLimitedRequestsHaveIdenticalAcknowledgements()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var unknown = await fixture.RequestAsync("unknown@example.org");
        Assert.IsEmpty(fixture.Email.Messages);
        using var known = await fixture.RequestAsync();
        using var limited = await fixture.RequestAsync();

        Assert.AreEqual(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.AreEqual(unknown.StatusCode, known.StatusCode);
        Assert.AreEqual(known.StatusCode, limited.StatusCode);
        Assert.AreEqual(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
        Assert.AreEqual(await known.Content.ReadAsStringAsync(), await limited.Content.ReadAsStringAsync());
        Assert.HasCount(1, fixture.Email.Messages);
        Assert.AreEqual("no-store", known.Headers.CacheControl?.ToString());
    }

    [TestMethod]
    public async Task LimitsUseSixtySecondAndRollingHourBoundariesAcrossNormalizedEmails()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var first = await fixture.RequestAsync();
        var expected = await first.Content.ReadAsStringAsync();
        fixture.Host.Clock.Advance(TimeSpan.FromSeconds(59));
        using var early = await fixture.RequestAsync("person@EXAMPLE.org");
        Assert.HasCount(1, fixture.Email.Messages);
        Assert.AreEqual(expected, await early.Content.ReadAsStringAsync());
        fixture.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        using var boundary = await fixture.RequestAsync();
        Assert.HasCount(2, fixture.Email.Messages);
        for (var index = 0; index < 3; index++)
        {
            fixture.Host.Clock.Advance(TimeSpan.FromMinutes(1));
            using var accepted = await fixture.RequestAsync();
            Assert.AreEqual(expected, await accepted.Content.ReadAsStringAsync());
        }

        fixture.Host.Clock.Advance(TimeSpan.FromMinutes(1));
        using var sixth = await fixture.RequestAsync();
        Assert.HasCount(5, fixture.Email.Messages);
        Assert.AreEqual(expected, await sixth.Content.ReadAsStringAsync());
        fixture.Host.Clock.Advance(TimeSpan.FromMinutes(54) + TimeSpan.FromSeconds(59));
        using var beforeHour = await fixture.RequestAsync();
        Assert.HasCount(5, fixture.Email.Messages);
        fixture.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        using var atHour = await fixture.RequestAsync();
        Assert.HasCount(6, fixture.Email.Messages);
        Assert.AreEqual(expected, await atHour.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task ConcurrentRecoveryRequestsDispatchOnlyOneMessage()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => fixture.RequestAsync()));
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
            }
        }

        Assert.HasCount(1, fixture.Email.Messages);
        Assert.AreEqual(1, await fixture.Host.WithAsync<AuthDbContext, int>(db => db.RecoveryRequests.CountAsync()));
    }

    [TestMethod]
    [DataRow("Failed")]
    [DataRow("Unknown")]
    [DataRow("Exception")]
    [DataRow("Timeout")]
    public async Task DeliveryFailurePreservesCredentialsSessionsAndAllowsImmediateRetry(string outcome)
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var session = await fixture.SignInAsync();
        using var unknown = await fixture.RequestAsync("unknown@example.org");
        fixture.Email.Mode = outcome;
        using var failed = await fixture.RequestAsync();
        Assert.AreEqual(HttpStatusCode.Accepted, failed.StatusCode);
        Assert.AreEqual(await unknown.Content.ReadAsStringAsync(), await failed.Content.ReadAsStringAsync());
        Assert.AreEqual(0, await fixture.Host.WithAsync<AuthDbContext, int>(db => db.RecoveryRequests.CountAsync()));
        await fixture.AssertPasswordAsync("Password1!", "NewPassword2!");
        Assert.AreEqual(HttpStatusCode.OK, (await session.GetAsync("/api/v1/me/session")).StatusCode);
        fixture.Email.Mode = "Accepted";
        using var retry = await fixture.RequestAsync();
        Assert.HasCount(2, fixture.Email.Messages);
        Assert.AreEqual(1, await fixture.Host.WithAsync<AuthDbContext, int>(db => db.RecoveryRequests.CountAsync()));
        fixture.AssertRedacted();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Aa1!b")]
    [DataRow("abcdef1!")]
    [DataRow("ABCDEF1!")]
    [DataRow("Abcdefg!")]
    [DataRow("Abcdefg1")]
    public async Task InvalidPasswordNamesFieldAndDoesNotConsumeToken(string password)
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var requested = await fixture.RequestAsync();
        var token = fixture.Token;
        using var rejected = await fixture.ResetAsync(password: password);
        var answer = await rejected.Content.ReadFromJsonAsync<ApiResponse<PasswordResetResponse>>();
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.AreEqual("VALIDATION_FAILED", answer?.Error?.Code);
        Assert.IsTrue(answer?.Error?.Fields?.ContainsKey("newPassword"));
        await fixture.AssertPasswordAsync("Password1!", "NewPassword2!");
        using var corrected = await fixture.ResetAsync(token, "Aa1!bc");
        Assert.AreEqual(HttpStatusCode.OK, corrected.StatusCode);
        await fixture.AssertPasswordAsync("Aa1!bc", "Password1!");
    }

    [TestMethod]
    public async Task TokenWorksImmediatelyBeforeExpiryAndCannotBeReused()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var requested = await fixture.RequestAsync();
        fixture.Host.Clock.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromTicks(1));
        using var accepted = await fixture.ResetAsync();
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
        using var replay = await fixture.ResetAsync(password: "OtherPassword3!");
        Assert.AreEqual("RESET_TOKEN_INVALID", (await replay.Content.ReadFromJsonAsync<ApiResponse<PasswordResetResponse>>())?.Error?.Code);
        await fixture.AssertPasswordAsync("NewPassword2!", "OtherPassword3!");
    }

    [TestMethod]
    public async Task ExpiredInvalidAndWrongAccountTokensPreservePasswordAndSessions()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var session = await fixture.SignInAsync();
        using var requested = await fixture.RequestAsync();
        using var invalid = await fixture.ResetAsync("invalid-token");
        using var wrongAccount = await fixture.ResetAsync(email: "unknown@example.org");
        fixture.Host.Clock.Advance(TimeSpan.FromMinutes(30));
        using var expired = await fixture.ResetAsync();
        foreach (var response in new[] { invalid, wrongAccount, expired })
        {
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.AreEqual("RESET_TOKEN_INVALID", (await response.Content.ReadFromJsonAsync<ApiResponse<PasswordResetResponse>>())?.Error?.Code);
        }

        await fixture.AssertPasswordAsync("Password1!", "NewPassword2!");
        Assert.AreEqual(HttpStatusCode.OK, (await session.GetAsync("/api/v1/me/session")).StatusCode);
        fixture.AssertRedacted();
    }

    [TestMethod]
    public async Task ConcurrentResetsConsumeTheTokenOnce()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var requested = await fixture.RequestAsync();
        var responses = await Task.WhenAll(fixture.ResetAsync(), fixture.ResetAsync());
        Assert.HasCount(1, responses.Where(response => response.StatusCode == HttpStatusCode.OK));
        Assert.HasCount(1, responses.Where(response => response.StatusCode == HttpStatusCode.UnprocessableEntity));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [TestMethod]
    public async Task FailedPasswordWritePreservesStampPasswordAndSessionsAndTokenCanBeRetried()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var session = await fixture.SignInAsync();
        using var requested = await fixture.RequestAsync();
        var originalStamp = await fixture.Host.WithAsync<AuthDbContext, string?>(db => db.Users.Select(user => user.SecurityStamp).SingleAsync());
        await fixture.Host.WithAsync<AuthDbContext>(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailRecoveryPasswordWrite ON AspNetUsers INSTEAD OF UPDATE AS THROW 51000, 'Test password write failure', 1;"));

        using var failed = await fixture.ResetAsync();
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.AreEqual("NOT_RECORDED", (await failed.Content.ReadFromJsonAsync<ApiResponse<PasswordResetResponse>>())?.Error?.Code);
        Assert.AreEqual(originalStamp, await fixture.Host.WithAsync<AuthDbContext, string?>(db => db.Users.Select(user => user.SecurityStamp).SingleAsync()));
        await fixture.AssertPasswordAsync("Password1!", "NewPassword2!");
        Assert.AreEqual(HttpStatusCode.OK, (await session.GetAsync("/api/v1/me/session")).StatusCode);
        fixture.AssertRedacted();

        await fixture.Host.WithAsync<AuthDbContext>(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailRecoveryPasswordWrite"));
        using var retried = await fixture.ResetAsync();
        Assert.AreEqual(HttpStatusCode.OK, retried.StatusCode);
        await fixture.AssertPasswordAsync("NewPassword2!", "Password1!");
    }

    [TestMethod]
    public async Task PasswordStorageAndEveryResponseExcludeCredentialsAndRecoveryLogsAreRedacted()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        using var request = await fixture.RequestAsync();
        using var invalid = await fixture.ResetAsync("broken-sensitive-token");
        using var badPassword = await fixture.ResetAsync(password: "short-sensitive-password");
        using var success = await fixture.ResetAsync();
        foreach (var response in new[] { request, invalid, badPassword, success })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Person@example.org", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.Token, body, StringComparison.Ordinal);
            Assert.DoesNotContain("NewPassword2!", body, StringComparison.Ordinal);
        }

        await fixture.AssertPasswordAsync("NewPassword2!", "Password1!");
        fixture.AssertRedacted();
    }

    [TestMethod]
    public async Task EndpointsRejectMalformedRequestsAndAuthenticatedCallers()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        foreach (var path in new[] { "/api/v1/auth/recoveries", "/api/v1/auth/password-resets" })
        {
            using var malformed = await fixture.Client.PostAsync(path, new StringContent("not-json"));
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, malformed.StatusCode);
            using var malformedJson = await fixture.Client.PostAsync(path,
                new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, malformedJson.StatusCode);
        }

        using var invalidEmail = await fixture.RequestAsync("invalid-email");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, invalidEmail.StatusCode);
        Assert.IsTrue((await invalidEmail.Content.ReadFromJsonAsync<ApiResponse<RecoveryAcknowledgement>>())?.Error?.Fields?.ContainsKey("email"));
        using var session = await fixture.SignInAsync();
        using var refusedRecovery = await session.PostAsJsonAsync("/api/v1/auth/recoveries", new RequestRecoveryRequest("Person@example.org"));
        using var refusedReset = await session.PostAsJsonAsync("/api/v1/auth/password-resets",
            new ResetPasswordRequest("Person@example.org", "unused", "NewPassword2!"));
        Assert.AreEqual(HttpStatusCode.Forbidden, refusedRecovery.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, refusedReset.StatusCode);
        Assert.IsEmpty(fixture.Email.Messages);
    }

    [TestMethod]
    public async Task UnconfiguredLinkOriginKeepsAcknowledgementAndSendsNothing()
    {
        await using var fixture = await RecoveryFixture.CreateAsync();
        fixture.Host.Factory.Services.GetRequiredService<IConfiguration>()["Authentication:PublicBaseUrl"] = null;
        using var request = await fixture.RequestAsync();
        Assert.AreEqual(HttpStatusCode.Accepted, request.StatusCode);
        Assert.IsEmpty(fixture.Email.Messages);
        await fixture.AssertPasswordAsync("Password1!", "NewPassword2!");
    }

    private sealed class RecoveryFixture(SqlTestHost host, FakeEmail email, CapturedLogs logs, HttpClient client) : IAsyncDisposable
    {
        public SqlTestHost Host { get; } = host;
        public FakeEmail Email { get; } = email;
        public HttpClient Client { get; } = client;
        public Uri ResetLink => new(Email.Messages.Last().PlainTextBody.Split('\n').Last());
        public string Token => HttpUtility.ParseQueryString(ResetLink.Fragment.TrimStart('#'))["token"]!;

        public static async Task<RecoveryFixture> CreateAsync()
        {
            var email = new FakeEmail();
            var logs = new CapturedLogs();
            var host = await SqlTestHost.CreateAsync(new DateTimeOffset(2026, 11, 18, 18, 0, 0, TimeSpan.Zero), services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(email);
                services.AddSingleton<ILoggerProvider>(logs);
            });
            host.Factory.Services.GetRequiredService<IConfiguration>()["Authentication:PublicBaseUrl"] = "https://refrog.example/";
            await using var scope = host.Factory.Services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
            var result = await accounts.CreateAsync(new AuthAccount
            {
                Id = Guid.NewGuid(),
                UserName = "Person@example.org",
                Email = "Person@example.org",
                EmailConfirmed = true,
                LockoutEnabled = true,
            }, "Password1!");
            Assert.IsTrue(result.Succeeded);
            return new(host, email, logs, host.CreateClient());
        }

        public Task<HttpResponseMessage> RequestAsync(string email = "Person@example.org") =>
            Client.PostAsJsonAsync("/api/v1/auth/recoveries", new RequestRecoveryRequest(email));

        public Task<HttpResponseMessage> ResetAsync(string? token = null, string password = "NewPassword2!", string email = "Person@example.org") =>
            Client.PostAsJsonAsync("/api/v1/auth/password-resets", new ResetPasswordRequest(email, token ?? Token, password));

        public async Task<HttpClient> SignInAsync()
        {
            var session = Host.CreateClient();
            using var response = await session.PostAsJsonAsync("/api/v1/auth/session", new SignInRequest("Person@example.org", "Password1!"));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            return session;
        }

        public async Task AssertPasswordAsync(string accepted, string refused)
        {
            await using var scope = Host.Factory.Services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
            var account = await accounts.FindByEmailAsync("Person@example.org");
            Assert.IsNotNull(account);
            Assert.IsTrue(await accounts.CheckPasswordAsync(account, accepted));
            Assert.IsFalse(await accounts.CheckPasswordAsync(account, refused));
            Assert.AreNotEqual(accepted, account.PasswordHash);
            var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AuthAccount>>();
            Assert.AreNotEqual(PasswordVerificationResult.Failed, hash.VerifyHashedPassword(account, account.PasswordHash!, accepted));
        }

        public void AssertRedacted()
        {
            var captured = string.Join('\n', logs.Messages);
            foreach (var secret in new[] { "Person@example.org", "Password1!", "NewPassword2!", "provider-sensitive-detail", Token })
            {
                Assert.DoesNotContain(secret, captured, StringComparison.OrdinalIgnoreCase);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Host.DisposeAsync();
        }
    }

    private sealed class FakeEmail : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Messages { get; } = new();
        public string Mode { get; set; } = "Accepted";

        public async Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Enqueue(message);
            await Task.Yield();
            return Mode switch
            {
                "Exception" => throw new InvalidOperationException("provider-sensitive-detail"),
                "Timeout" => throw new TaskCanceledException("provider-sensitive-detail"),
                "Failed" => EmailDeliveryOutcome.Failed,
                "Unknown" => EmailDeliveryOutcome.Unknown,
                _ => EmailDeliveryOutcome.Accepted,
            };
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception) + exception);
        }
    }
}
