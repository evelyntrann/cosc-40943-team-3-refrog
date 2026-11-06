using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed partial class RegistrationFlowTests
{
    [TestMethod]
    public async Task RegistrationCreatesAccountOnlyAfterCodeAndPersistsExactTcuVerification()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var start = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("student@tcu.edu", "Password1!", Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.Accepted, start.StatusCode);
        var pending = await start.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.IsNotNull(pending?.Data);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.AreEqual(0, await db.Users.CountAsync());
            Assert.AreEqual(0, await db.TcuEmailVerifications.CountAsync());
        }

        var verification = await fixture.Client.PostAsJsonAsync(
            $"/api/v1/auth/registrations/{pending.Data.RegistrationId}/verification",
            new VerifyRegistrationRequest(fixture.Email.LastCode));
        Assert.AreEqual(HttpStatusCode.OK, verification.StatusCode);
        var completed = await verification.Content.ReadFromJsonAsync<ApiResponse<RegisteredAccountResponse>>();
        Assert.IsTrue(completed?.Data?.TcuEmailVerified);
        Assert.IsTrue(verification.Headers.TryGetValues("Set-Cookie", out var cookies));
        StringAssert.Contains(string.Join(';', cookies), "secure", StringComparison.OrdinalIgnoreCase);
        StringAssert.Contains(string.Join(';', cookies), "httponly", StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(fixture.Clock.GetUtcNow().AddDays(30), CookieExpiry(cookies));

        await using var finalScope = fixture.Factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.AreEqual(1, await finalDb.Users.CountAsync());
        Assert.AreEqual(1, await finalDb.TcuEmailVerifications.CountAsync());
        Assert.AreNotEqual("Password1!", (await finalDb.Users.SingleAsync()).PasswordHash);
    }

    [TestMethod]
    public async Task ResendSupersedesEarlierCodeEvenWhenDeliveryFails()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var start = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("person@example.org", "Password1!", Guid.NewGuid()));
        var pending = (await start.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>())!.Data!;
        var oldCode = fixture.Email.LastCode;

        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        fixture.Email.Outcome = EmailDeliveryOutcome.Failed;
        var resend = await fixture.Client.PostAsJsonAsync($"/api/v1/auth/registrations/{pending.RegistrationId}/codes",
            new ResendRegistrationCodeRequest(Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, resend.StatusCode);
        Assert.AreNotEqual(oldCode, fixture.Email.LastCode);

        var oldVerification = await fixture.Client.PostAsJsonAsync(
            $"/api/v1/auth/registrations/{pending.RegistrationId}/verification", new VerifyRegistrationRequest(oldCode));
        Assert.AreNotEqual(HttpStatusCode.OK, oldVerification.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync());
    }

    [TestMethod]
    public async Task InvalidInputNamesFieldsWithoutCreatingPendingState()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("person@example.org", "short", Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.AreEqual("VALIDATION_FAILED", body?.Error?.Code);
        Assert.IsTrue(body?.Error?.Fields?.ContainsKey("password"));
        Assert.AreEqual(0, fixture.Email.Count);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().PendingRegistrations.CountAsync());
    }

    [TestMethod]
    public async Task StartRequestIdIsIdempotentAndChangedInputIsRejected()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var requestId = Guid.NewGuid();
        var first = new StartRegistrationRequest("person@example.org", "Password1!", requestId);
        var accepted = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations", first);
        var replayed = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations", first);
        var reused = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            first with { Password = "Different1!" });

        Assert.AreEqual(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, replayed.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.AreEqual(1, fixture.Email.Count);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().PendingRegistrations.CountAsync());
    }

    [TestMethod]
    public async Task DuplicateEmailCannotCreateAnotherAccount()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var completed = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, completed.StatusCode);

        using var signedOut = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false,
        });
        var duplicate = await signedOut.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("PERSON@example.org", "Password1!", Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.AreEqual(1, fixture.Email.Count);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync());
    }

    [TestMethod]
    public async Task RequestLimitsUseTheFixedClockAndProvideRetryTime()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var route = $"/api/v1/auth/registrations/{pending.RegistrationId}/codes";
        fixture.Clock.Advance(TimeSpan.FromSeconds(59));
        var early = await fixture.Client.PostAsJsonAsync(route, new ResendRegistrationCodeRequest(Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.TooManyRequests, early.StatusCode);
        var earlyBody = await early.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.IsNotNull(earlyBody?.Error?.RetryAt);

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        for (var index = 0; index < 4; index++)
        {
            var accepted = await fixture.Client.PostAsJsonAsync(route, new ResendRegistrationCodeRequest(Guid.NewGuid()));
            Assert.AreEqual(HttpStatusCode.Accepted, accepted.StatusCode);
            fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        }

        var sixth = await fixture.Client.PostAsJsonAsync(route, new ResendRegistrationCodeRequest(Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.AreEqual(5, fixture.Email.Count);
        fixture.Clock.Advance(TimeSpan.FromMinutes(56));
        var afterWindow = await fixture.Client.PostAsJsonAsync(route, new ResendRegistrationCodeRequest(Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.Accepted, afterWindow.StatusCode);
    }

    [TestMethod]
    public async Task FiveIncorrectCodesInvalidateTheChallenge()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var correct = fixture.Email.LastCode;
        var wrong = correct == "000000" ? "000001" : "000000";
        for (var index = 0; index < 5; index++)
        {
            var failed = await fixture.VerifyAsync(pending.RegistrationId, wrong);
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
        }

        var old = await fixture.VerifyAsync(pending.RegistrationId, correct);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, old.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync());
    }

    [TestMethod]
    public async Task CodeExpiresAtTenMinutes()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var code = fixture.Email.LastCode;
        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        var expired = await fixture.VerifyAsync(pending.RegistrationId, code);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
        var body = await expired.Content.ReadFromJsonAsync<ApiResponse<RegisteredAccountResponse>>();
        Assert.AreEqual("CODE_EXPIRED", body?.Error?.Code);
    }

    [TestMethod]
    public async Task AdministratorBootstrapUsesFourHourCookie()
    {
        await using var fixture = await RegistrationFixture.CreateAsync("admin@tcu.edu");
        var pending = await fixture.StartAsync("admin@tcu.edu");
        var completed = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, completed.StatusCode);
        Assert.IsTrue(completed.Headers.TryGetValues("Set-Cookie", out var cookies));
        var cookie = string.Join(';', cookies);
        StringAssert.Contains(cookie, "expires=", StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(fixture.Clock.GetUtcNow().AddHours(4), CookieExpiry(cookies));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AuthAccount>>();
        var account = await userManager.FindByEmailAsync("admin@tcu.edu");
        Assert.IsNotNull(account);
        Assert.IsTrue(await userManager.IsInRoleAsync(account, "Administrator"));
    }

    [TestMethod]
    public async Task MalformedJsonUsesTheSharedValidationEnvelope()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        var response = await fixture.Client.PostAsync("/api/v1/auth/registrations", content);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.AreEqual(ApiErrorCategory.Validation, body?.Error?.Category);
    }

    [TestMethod]
    public async Task MalformedCodeDoesNotConsumeAnAttempt()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var malformed = await fixture.VerifyAsync(pending.RegistrationId, "12x");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, malformed.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().VerificationFailures.CountAsync());
    }

    [TestMethod]
    public async Task CodeCannotCompleteAnotherPendingRegistration()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var first = await fixture.StartAsync("first@example.org");
        var firstCode = fixture.Email.LastCode;
        var second = await fixture.StartAsync("second@example.org");
        var secondCode = fixture.Email.LastCode;

        var crossed = await fixture.VerifyAsync(second.RegistrationId, firstCode);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, crossed.StatusCode);
        var correct = await fixture.VerifyAsync(second.RegistrationId, secondCode);
        Assert.AreEqual(HttpStatusCode.OK, correct.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync());
        Assert.AreNotEqual(first.RegistrationId, second.RegistrationId);
    }

    [TestMethod]
    public async Task NonTcuRegistrationDoesNotEstablishAffiliation()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var result = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<ApiResponse<RegisteredAccountResponse>>();
        Assert.IsFalse(body?.Data?.TcuEmailVerified);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().TcuEmailVerifications.CountAsync());
    }

    [TestMethod]
    public async Task DestinationEmailLimitAppliesAcrossPendingRegistrations()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        for (var index = 0; index < 5; index++)
        {
            await fixture.StartAsync("person@example.org");
        }

        var sixth = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("person@example.org", "Password1!", Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.AreEqual(5, fixture.Email.Count);
        var body = await sixth.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.IsNotNull(body?.Error?.RetryAt);
    }

    [TestMethod]
    public async Task RollingIncorrectLimitSurvivesResends()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var route = $"/api/v1/auth/registrations/{pending.RegistrationId}/codes";
        for (var round = 0; round < 2; round++)
        {
            var correct = fixture.Email.LastCode;
            var wrong = correct == "000000" ? "000001" : "000000";
            for (var index = 0; index < 5; index++)
            {
                var failed = await fixture.VerifyAsync(pending.RegistrationId, wrong);
                Assert.AreEqual(HttpStatusCode.UnprocessableEntity, failed.StatusCode);
            }

            fixture.Clock.Advance(TimeSpan.FromSeconds(60));
            var resent = await fixture.Client.PostAsJsonAsync(route, new ResendRegistrationCodeRequest(Guid.NewGuid()));
            Assert.AreEqual(HttpStatusCode.Accepted, resent.StatusCode);
        }

        var blocked = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        fixture.Clock.Advance(TimeSpan.FromMinutes(15));
        var afterWindow = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, afterWindow.StatusCode);
    }

    [TestMethod]
    public async Task TwoConcurrentCompletionsCreateOneAccount()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var code = fixture.Email.LastCode;
        using var firstClient = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false,
        });
        using var secondClient = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false,
        });
        var route = $"/api/v1/auth/registrations/{pending.RegistrationId}/verification";
        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync(route, new VerifyRegistrationRequest(code)),
            secondClient.PostAsJsonAsync(route, new VerifyRegistrationRequest(code)));

        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync());
    }

    [TestMethod]
    public async Task FailedAffiliationWriteRollsBackAccountAndSession()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@tcu.edu");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER FailTestVerificationWrite ON TcuEmailVerifications INSTEAD OF INSERT AS THROW 51000, 'Test write failure', 1;");
        }

        var result = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.IsFalse(result.Headers.Contains("Set-Cookie"));
        await using var finalScope = fixture.Factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.AreEqual(0, await finalDb.Users.CountAsync());
        Assert.AreEqual(0, await finalDb.TcuEmailVerifications.CountAsync());
    }

    [TestMethod]
    public async Task FailedInitialDeliveryKeepsPendingRegistrationAndNeverClaimsSent()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        fixture.Email.Outcome = EmailDeliveryOutcome.Failed;
        var request = new StartRegistrationRequest("person@example.org", "Password1!", Guid.NewGuid());
        var failed = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations", request);
        var replayed = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations", request);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, replayed.StatusCode);
        Assert.AreEqual(1, fixture.Email.Count);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Assert.AreEqual(1, await db.PendingRegistrations.CountAsync());
        Assert.AreEqual(0, await db.Users.CountAsync());
    }

    [TestMethod]
    public async Task CompletedCodeCannotBeReused()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var code = fixture.Email.LastCode;
        var first = await fixture.VerifyAsync(pending.RegistrationId, code);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);

        using var signedOut = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false,
        });
        var second = await signedOut.PostAsJsonAsync(
            $"/api/v1/auth/registrations/{pending.RegistrationId}/verification", new VerifyRegistrationRequest(code));
        Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);
    }

    [TestMethod]
    public async Task AuthenticatedAccountCannotStartAnotherRegistration()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        var completed = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, completed.StatusCode);

        Assert.IsTrue(completed.Headers.TryGetValues("Set-Cookie", out var cookieHeaders));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/registrations")
        {
            Content = JsonContent.Create(new StartRegistrationRequest("other@example.org", "Password1!", Guid.NewGuid())),
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookieHeaders.First().Split(';')[0]);
        var refused = await fixture.Client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Forbidden, refused.StatusCode);
        var body = await refused.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.AreEqual(ApiErrorCategory.Authorization, body?.Error?.Category);
        Assert.AreEqual(1, fixture.Email.Count);
    }

    [TestMethod]
    public async Task AccountCannotSignInUntilCodeIsVerified()
    {
        // FR-AUTH-account-signup verification, checked at Identity's credential check;
        // the HTTP sign-in refusal arrives with UC-AUTH-sign-in.
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
            Assert.IsNull(await accounts.FindByEmailAsync("person@example.org"));
        }

        var completed = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, completed.StatusCode);

        await using var finalScope = fixture.Factory.Services.CreateAsyncScope();
        var finalAccounts = finalScope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
        var account = await finalAccounts.FindByEmailAsync("person@example.org");
        Assert.IsNotNull(account);
        Assert.IsTrue(await finalAccounts.CheckPasswordAsync(account, "Password1!"));
        Assert.IsFalse(await finalAccounts.CheckPasswordAsync(account, "Wrong1!"));
    }

    [TestMethod]
    public async Task ResendWithTheSameRequestIdSendsOnce()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        var route = $"/api/v1/auth/registrations/{pending.RegistrationId}/codes";
        var request = new ResendRegistrationCodeRequest(Guid.NewGuid());

        var first = await fixture.Client.PostAsJsonAsync(route, request);
        var codeAfterFirst = fixture.Email.LastCode;
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        var retried = await fixture.Client.PostAsJsonAsync(route, request);

        Assert.AreEqual(HttpStatusCode.Accepted, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, retried.StatusCode);
        Assert.AreEqual(2, fixture.Email.Count);
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        var retriedBody = await retried.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.AreEqual(firstBody?.Data, retriedBody?.Data);
        var verified = await fixture.VerifyAsync(pending.RegistrationId, codeAfterFirst);
        Assert.AreEqual(HttpStatusCode.OK, verified.StatusCode);
    }

    [TestMethod]
    public async Task PasswordIsStoredOnlyAsHashAndNeverReturnedOrLogged()
    {
        const string password = "Sup3r-Secret!";
        await using var fixture = await RegistrationFixture.CreateAsync();
        var start = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("hidden@example.org", password, Guid.NewGuid()));
        var startBody = await start.Content.ReadAsStringAsync();
        var pending = JsonSerializer.Deserialize<ApiResponse<RegistrationChallengeResponse>>(startBody, WebJson)!.Data!;
        var code = fixture.Email.LastCode;
        var verify = await fixture.VerifyAsync(pending.RegistrationId, code);
        var verifyBody = await verify.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, verify.StatusCode);

        foreach (var body in new[] { startBody, verifyBody })
        {
            Assert.IsFalse(body.Contains(password, StringComparison.Ordinal));
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AuthAccount>>();
        var stored = new[] { (await db.PendingRegistrations.SingleAsync()).PasswordHash, (await db.Users.SingleAsync()).PasswordHash! };
        foreach (var hash in stored)
        {
            Assert.IsFalse(hash.Contains(password, StringComparison.Ordinal));
            Assert.AreNotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(new AuthAccount(), hash, password));
        }

        foreach (var line in fixture.Logs.Messages)
        {
            Assert.IsFalse(line.Contains(password, StringComparison.Ordinal), "Password was logged.");
            Assert.IsFalse(line.Contains("hidden@example.org", StringComparison.OrdinalIgnoreCase), "Email was logged.");
            Assert.IsFalse(line.Contains(code, StringComparison.Ordinal), "Verification code was logged.");
        }
    }

    [TestMethod]
    public async Task EmailWithApostropheCompletesRegistration()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("o'brien@example.org");
        var completed = await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, completed.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync());
    }

    [TestMethod]
    public async Task EmailLongerThanStorageIsAValidationFailure()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var label = new string('b', 60);
        var tooLong = $"{new string('a', 64)}@{label}.{label}.{label}.example.org";
        Assert.IsTrue(tooLong.Length > 256 && System.Net.Mail.MailAddress.TryCreate(tooLong, out _));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest(tooLong, "Password1!", Guid.NewGuid()));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>();
        Assert.IsTrue(body?.Error?.Fields?.ContainsKey("email"));
        Assert.AreEqual(0, fixture.Email.Count);
    }

    [TestMethod]
    public async Task ChallengeOwnerMustMatchItsPurpose()
    {
        // Shared code isolation at the storage level: a registration challenge cannot belong to an
        // account and an affiliation challenge cannot belong to a pending registration.
        // Crossing purposes through the API waits for UC-IDV-verify-affiliation.
        await using var fixture = await RegistrationFixture.CreateAsync();
        var pending = await fixture.StartAsync("person@example.org");
        await fixture.VerifyAsync(pending.RegistrationId, fixture.Email.LastCode);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var accountId = (await db.Users.SingleAsync()).Id;
        foreach (var (purpose, registrationId, account) in new[]
        {
            ("TCU_AFFILIATION", (Guid?)pending.RegistrationId, (Guid?)null),
            ("ACCOUNT_REGISTRATION", null, accountId),
        })
        {
            db.ChangeTracker.Clear();
            db.VerificationChallenges.Add(new VerificationChallenge
            {
                Id = Guid.NewGuid(),
                Purpose = purpose,
                PendingRegistrationId = registrationId,
                AccountId = account,
                NormalizedEmail = "PERSON@EXAMPLE.ORG",
                CodeHmac = new byte[32],
                KeyVersion = 1,
                RequestId = Guid.NewGuid(),
                CreatedAt = fixture.Clock.GetUtcNow(),
                ExpiresAt = fixture.Clock.GetUtcNow().AddMinutes(10),
            });
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [TestMethod]
    public async Task ServerServesBlazorShellForRegistrationRoute()
    {
        await using var fixture = await RegistrationFixture.CreateAsync();
        var page = await fixture.Client.GetAsync("/register");
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        var content = await page.Content.ReadAsStringAsync();
        StringAssert.Contains(content, "ReFrog");
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"\b\d{6}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    private static DateTimeOffset CookieExpiry(IEnumerable<string> headers)
    {
        var part = headers.SelectMany(header => header.Split(';'))
            .Single(value => value.TrimStart().StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        return DateTimeOffset.Parse(part.Split('=', 2)[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class CaptureEmailSender : IEmailSender
    {
        public EmailDeliveryOutcome Outcome { get; set; } = EmailDeliveryOutcome.Accepted;
        public int Count { get; private set; }
        public string LastCode { get; private set; } = string.Empty;

        public Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Count++;
            LastCode = CodePattern().Match(message.PlainTextBody).Value;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> messages = new();

        public IEnumerable<string> Messages => messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + " " + exception);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan elapsed) => now += elapsed;
    }

    private sealed class RegistrationFixture : IAsyncDisposable
    {
        private RegistrationFixture(WebApplicationFactory<Program> factory, HttpClient client, CaptureEmailSender email,
            ManualClock clock, CapturingLoggerProvider logs)
        {
            Factory = factory;
            Client = client;
            Email = email;
            Clock = clock;
            Logs = logs;
        }

        public CapturingLoggerProvider Logs { get; }

        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }
        public CaptureEmailSender Email { get; }
        public ManualClock Clock { get; }

        public async Task<RegistrationChallengeResponse> StartAsync(string address)
        {
            var response = await Client.PostAsJsonAsync("/api/v1/auth/registrations",
                new StartRegistrationRequest(address, "Password1!", Guid.NewGuid()));
            Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>())!.Data!;
        }

        public Task<HttpResponseMessage> VerifyAsync(Guid registrationId, string code) =>
            Client.PostAsJsonAsync($"/api/v1/auth/registrations/{registrationId}/verification", new VerifyRegistrationRequest(code));

        public static async Task<RegistrationFixture> CreateAsync(string? administratorEmail = null)
        {
            var source = Environment.GetEnvironmentVariable("REFROG_TEST_SQL");
            if (string.IsNullOrWhiteSpace(source))
            {
                Assert.Inconclusive("Set REFROG_TEST_SQL to a disposable SQL Server test instance.");
            }

            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(source)
            {
                InitialCatalog = $"ReFrogAuthTest_{Guid.NewGuid():N}",
            };
            var email = new CaptureEmailSender();
            var clock = new ManualClock();
            var logs = new CapturingLoggerProvider();
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            {
                host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ReFrog"] = builder.ConnectionString,
                        ["Authentication:VerificationKeys:1"] = key,
                        ["Authentication:ActiveVerificationKeyVersion"] = "1",
                    }));
                if (administratorEmail is not null)
                {
                    host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["Authentication:AdministratorEmails:0"] = administratorEmail }));
                }
                host.ConfigureServices(services =>
                {
                    services.RemoveAll<IEmailSender>();
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<IEmailSender>(email);
                    services.AddSingleton<TimeProvider>(clock);
                    services.AddSingleton<ILoggerProvider>(logs);
                });
            });
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost/"),
                AllowAutoRedirect = false,
            });
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
            return new(factory, client, email, clock, logs);
        }

        public async ValueTask DisposeAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureDeletedAsync();
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }
}
