using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Contracts.IdentityVerification;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.IdentityVerification;
using ReFrog.Server.Features.Volunteer;
using ReFrog.Server.Tests.Support;
using static ReFrog.Server.Tests.IdentityVerification.AffiliationApi;

namespace ReFrog.Server.Tests.IdentityVerification;

// UC-IDV-verify-affiliation: main flow, extensions 2a-2d, 3a, 5a, and the IDV.md Tests rows named on each test.
[TestClass]
public sealed class AffiliationFlowTests
{
    private const string Shopper = "shopper@example.org";
    private const string TcuEmail = "kim.frog@tcu.edu";

    // IDV.md row "UC-IDV-verify-affiliation main".
    [TestMethod]
    public async Task MainFlowSendsACodeVerifiesItAndMakesTheShopperEligible()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        var before = await EligibilityAsync(client);
        Assert.IsFalse(before.Eligible);
        Assert.AreEqual(VolunteerAccessValues.None, before.VolunteerAccess);

        using var requested = await RequestCodeAsync(client, TcuEmail);
        Assert.AreEqual(HttpStatusCode.Accepted, requested.StatusCode);
        var sent = (await requested.Content.ReadFromJsonAsync<ApiResponse<TcuCodeResponse>>())!.Data!;
        Assert.AreEqual(AffiliationFixture.Now + VerificationPolicy.CodeLifetime, sent.ExpiresAt);
        Assert.AreEqual(AffiliationFixture.Now + VerificationPolicy.ResendSpacing, sent.ResendAvailableAt);
        Assert.AreEqual(1, fixture.Email.Count);
        Assert.AreEqual(TcuEmail, fixture.Email.Sent.Single().Destination);
        Assert.AreEqual(sent.ExpiresAt, (await EligibilityAsync(client)).PendingCodeExpiresAt);

        using var verified = await VerifyAsync(client, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, verified.StatusCode);
        var result = (await verified.Content.ReadFromJsonAsync<ApiResponse<TcuVerificationResponse>>())!.Data!;
        Assert.IsTrue(result.TcuEmailVerified);
        Assert.IsTrue(result.Eligible);
        Assert.AreEqual(EligibilityBasis.TcuEmail, result.Basis);
        Assert.AreEqual(AffiliationFixture.Now, result.VerifiedAt);

        var row = await fixture.WithAuthAsync(db => db.TcuEmailVerifications.AsNoTracking().SingleAsync());
        Assert.AreEqual(account, row.AccountId);
        Assert.IsNotNull(await fixture.WithAuthAsync(db => db.VerificationChallenges.AsNoTracking()
            .Select(item => item.ConsumedAt).SingleAsync()));

        var after = await EligibilityAsync(client);
        Assert.IsTrue(after.Eligible);
        Assert.AreEqual(EligibilityBasis.TcuEmail, after.Basis);
        Assert.IsTrue(after.TcuEmailVerified);
        Assert.IsNull(after.PendingCodeExpiresAt);
    }

    // IDV.md row "main, persists across sessions" (FR-AUTH-affiliation-from-signin).
    [TestMethod]
    public async Task VerificationPersistsIntoANewSession()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using (var first = await fixture.SignedInClientAsync(Shopper))
        {
            await VerifiedThroughApiAsync(first, fixture, TcuEmail);
        }

        fixture.Clock.Advance(TimeSpan.FromDays(3));
        using var second = await fixture.SignedInClientAsync(Shopper);

        var status = await EligibilityAsync(second);
        Assert.IsTrue(status.Eligible);
        Assert.AreEqual(EligibilityBasis.TcuEmail, status.Basis);
        Assert.AreEqual(1, fixture.Email.Count, "no second code step");
    }

    // IDV.md row "main, wire format".
    [TestMethod]
    public async Task EnumsAndErrorCodesTravelAsStrings()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await VerifiedThroughApiAsync(client, fixture, TcuEmail);

        var body = await client.GetStringAsync("/api/v1/me/shopping-eligibility");
        StringAssert.Contains(body, "\"basis\":\"TCU_EMAIL\"");
        StringAssert.Contains(body, "\"volunteerAccess\":\"NONE\"");

        using var refused = await RequestCodeAsync(client, "a@tcu.edu");
        StringAssert.Contains(await refused.Content.ReadAsStringAsync(), "\"code\":\"ALREADY_VERIFIED\"");
        using var invalid = await VerifyAsync(client, "12a456");
        StringAssert.Contains(await invalid.Content.ReadAsStringAsync(), "\"code\":\"VALIDATION_FAILED\"");
    }

    // IDV.md row "2a".
    [TestMethod]
    public async Task AlreadyVerifiedShopperIsEligibleWithNoCodeStep()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);
        await SeedVerificationAsync(fixture, account, "KIM.FROG@TCU.EDU");
        using var client = await fixture.SignedInClientAsync(Shopper);

        var status = await EligibilityAsync(client);

        Assert.IsTrue(status.Eligible);
        Assert.AreEqual(EligibilityBasis.TcuEmail, status.Basis);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
    }

    // IDV.md rows "2b, role only" and "2b, role and email".
    [TestMethod]
    public async Task ActiveVolunteerIsEligibleAndEmailVerificationIsReportedSeparately()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync("volunteer@example.org", VolunteerAuthorization.VolunteerRole);
        var both = await fixture.CreateAccountAsync("both@example.org", VolunteerAuthorization.VolunteerRole);
        await SeedVerificationAsync(fixture, both, "BOTH@TCU.EDU");

        using var roleOnly = await fixture.SignedInClientAsync("volunteer@example.org");
        var volunteer = await EligibilityAsync(roleOnly);
        Assert.IsTrue(volunteer.Eligible);
        Assert.AreEqual(EligibilityBasis.VolunteerRole, volunteer.Basis);
        Assert.AreEqual(VolunteerAccessValues.RoleActive, volunteer.VolunteerAccess);
        Assert.IsFalse(volunteer.TcuEmailVerified);
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()), "no row written");

        using var bothClient = await fixture.SignedInClientAsync("both@example.org");
        var verifiedVolunteer = await EligibilityAsync(bothClient);
        Assert.IsTrue(verifiedVolunteer.Eligible);
        Assert.AreEqual(EligibilityBasis.VolunteerRole, verifiedVolunteer.Basis);
        Assert.IsTrue(verifiedVolunteer.TcuEmailVerified);
    }

    // IDV.md rows "2c, status" and "2c, writes".
    [TestMethod]
    public async Task RestrictedAccountIsNotEligibleAndCannotRequestOrSubmitCodes()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper, VolunteerAuthorization.VolunteerRestrictedRole);
        await SeedVerificationAsync(fixture, account, "KIM.FROG@TCU.EDU");
        await fixture.CreateAccountAsync("restricted2@example.org", VolunteerAuthorization.VolunteerRestrictedRole);
        using var client = await fixture.SignedInClientAsync(Shopper);

        var status = await EligibilityAsync(client);
        Assert.IsFalse(status.Eligible);
        Assert.IsNull(status.Basis);
        Assert.AreEqual(VolunteerAccessValues.Restricted, status.VolunteerAccess);
        Assert.IsTrue(status.TcuEmailVerified);

        using var unverified = await fixture.SignedInClientAsync("restricted2@example.org");
        using var request = await RequestCodeAsync(unverified, TcuEmail);
        Assert.AreEqual(HttpStatusCode.Forbidden, request.StatusCode);
        Assert.AreEqual("VOLUNTEER_ACCESS_INACTIVE", (await ErrorAsync(request))?.Code);
        using var verify = await VerifyAsync(unverified, "123456");
        Assert.AreEqual(HttpStatusCode.Forbidden, verify.StatusCode);
        Assert.AreEqual("VOLUNTEER_ACCESS_INACTIVE", (await ErrorAsync(verify))?.Code);

        Assert.AreEqual(0, fixture.Email.Count);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
    }

    // IDV.md row "2d": access state or the persisted-verification read unavailable.
    [TestMethod]
    public async Task UnavailableStateIsAccessUnavailableAndPreservesSavedVerification()
    {
        var faults = new Faults();
        await using var fixture = await AffiliationFixture.CreateAsync(faults.Install);
        var account = await fixture.CreateAccountAsync(Shopper);
        await SeedVerificationAsync(fixture, account, "KIM.FROG@TCU.EDU");
        using var client = await fixture.SignedInClientAsync(Shopper);

        foreach (var fail in new Action<bool>[] { on => faults.AccessFails = on, on => faults.ReaderFails = on })
        {
            fail(true);
            using var get = await client.GetAsync("/api/v1/me/shopping-eligibility");
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, get.StatusCode);
            Assert.AreEqual("ACCESS_UNAVAILABLE", (await ErrorAsync(get))?.Code);
            using var request = await RequestCodeAsync(client, TcuEmail);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, request.StatusCode);
            Assert.AreEqual("ACCESS_UNAVAILABLE", (await ErrorAsync(request))?.Code);
            fail(false);
        }

        // The verification POST reads access first, and its own challenge reads in the AUTH store.
        faults.AccessFails = true;
        using (var verify = await VerifyAsync(client, "123456"))
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, verify.StatusCode);
            Assert.AreEqual("ACCESS_UNAVAILABLE", (await ErrorAsync(verify))?.Code);
        }

        faults.AccessFails = false;
        faults.ChallengeReadsFail = true;
        using (var verify = await VerifyAsync(client, "123456"))
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, verify.StatusCode);
            Assert.AreEqual("ACCESS_UNAVAILABLE", (await ErrorAsync(verify))?.Code);
        }

        faults.ChallengeReadsFail = false;
        var saved = await fixture.WithAuthAsync(db => db.TcuEmailVerifications.AsNoTracking().SingleAsync());
        Assert.AreEqual(account, saved.AccountId);
        Assert.AreEqual(AffiliationFixture.Now, saved.VerifiedAt);
        Assert.AreEqual(0, fixture.Email.Count);

        var retry = await EligibilityAsync(client);
        Assert.IsTrue(retry.Eligible, "retry after recovery succeeds");

        // Code review finding 6 on PR #149: the failure reaches the logs for operators, without personal data.
        Assert.IsTrue(fixture.Logs.Entries.Any(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error &&
            entry.Text.StartsWith("AccessStateUnavailable ", StringComparison.Ordinal)));
        Assert.IsFalse(fixture.Logs.Messages.Any(line => line.Contains("KIM.FROG@TCU.EDU", StringComparison.OrdinalIgnoreCase)));
    }

    // IDV.md row "2d, cookie stage".
    [TestMethod]
    public async Task UnreadableAccountStateStopsAtTheCookieCheck()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await fixture.WithAuthAsync(db => db.Database.ExecuteSqlRawAsync(
            "EXEC sp_rename 'AspNetUsers.SecurityStamp', 'UnavailableStamp', 'COLUMN'"));
        try
        {
            using var get = await client.GetAsync("/api/v1/me/shopping-eligibility");
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, get.StatusCode);
            Assert.AreEqual("AUTHENTICATION_UNAVAILABLE", (await ErrorAsync(get))?.Code);
        }
        finally
        {
            await fixture.WithAuthAsync(db => db.Database.ExecuteSqlRawAsync(
                "EXEC sp_rename 'AspNetUsers.UnavailableStamp', 'SecurityStamp', 'COLUMN'"));
        }

        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()));
    }

    // IDV.md row "3a" at the endpoint: refused with 422 naming email, no row and no email.
    [TestMethod]
    [DataRow("a@frog.tcu.edu")]
    [DataRow("a@tcu.edu.co")]
    [DataRow("a@xtcu.edu")]
    [DataRow("a@b@tcu.edu")]
    [DataRow("not an address")]
    [DataRow("")]
    [DataRow(null)]
    public async Task NonTcuEmailIsRefusedNamingTheField(string? email)
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        using var response = await RequestCodeAsync(client, email);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await ErrorAsync(response);
        Assert.AreEqual("VALIDATION_FAILED", error?.Code);
        Assert.IsTrue(error?.Fields?.ContainsKey("email"));
        Assert.AreEqual(0, fixture.Email.Count);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
    }

    // IDV.md row "3a": case variants are the same normalized address.
    [TestMethod]
    public async Task CaseVariantsAreStoredAsOneNormalizedEmail()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        await fixture.CreateAccountAsync("other@example.org");
        using var lower = await fixture.SignedInClientAsync(Shopper);
        using var upper = await fixture.SignedInClientAsync("other@example.org");

        await VerifiedThroughApiAsync(lower, fixture, "a@tcu.edu");
        await VerifiedThroughApiAsync(upper, fixture, "A@TCU.EDU");

        var stored = await fixture.WithAuthAsync(db => db.TcuEmailVerifications.AsNoTracking()
            .Select(item => item.NormalizedEmail).Distinct().ToListAsync());
        Assert.HasCount(1, stored);
    }

    // IDV.md row "5a, wrong code".
    [TestMethod]
    public async Task WrongCodeIsCountedOnceAndVerifiesNothing()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);

        using var response = await VerifyAsync(client, WrongCode(fixture.Email.LastCode));

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.AreEqual("CODE_INVALID", (await ErrorAsync(response))?.Code);
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.SumAsync(item => item.FailedAttempts)));
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()));
    }

    // IDV.md row "5a, fifth failure".
    [TestMethod]
    public async Task FifthWrongCodeInvalidatesTheCode()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);
        var correct = fixture.Email.LastCode;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await VerifyAsync(client, WrongCode(correct));
            Assert.AreEqual("CODE_INVALID", (await ErrorAsync(wrong))?.Code);
        }

        using var response = await VerifyAsync(client, correct);
        Assert.AreEqual("CODE_EXPIRED", (await ErrorAsync(response))?.Code);
        Assert.IsNull((await EligibilityAsync(client)).PendingCodeExpiresAt);
    }

    // IDV.md row "5a, expiry boundary".
    [TestMethod]
    public async Task CorrectCodeIsAcceptedOneSecondBeforeExpiry()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);

        fixture.Clock.Advance(VerificationPolicy.CodeLifetime - TimeSpan.FromSeconds(1));
        using var response = await VerifyAsync(client, fixture.Email.LastCode);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task CorrectCodeIsExpiredAtExpiry()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);

        fixture.Clock.Advance(VerificationPolicy.CodeLifetime);
        using var response = await VerifyAsync(client, fixture.Email.LastCode);

        Assert.AreEqual("CODE_EXPIRED", (await ErrorAsync(response))?.Code);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
    }

    // IDV.md row "5a, resend".
    [TestMethod]
    public async Task ResendSupersedesTheOldCode()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);
        var old = fixture.Email.LastCode;

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        using var resend = await RequestCodeAsync(client, TcuEmail);
        Assert.AreEqual(HttpStatusCode.Accepted, resend.StatusCode);
        var current = fixture.Email.LastCode;
        Assert.AreNotEqual(old, current);

        using var oldAnswer = await VerifyAsync(client, old);
        Assert.AreEqual("CODE_EXPIRED", (await ErrorAsync(oldAnswer))?.Code);
        using var newAnswer = await VerifyAsync(client, current);
        Assert.AreEqual(HttpStatusCode.OK, newAnswer.StatusCode);
    }

    // IDV.md row "5a, failure budget survives resend".
    [TestMethod]
    public async Task FailureBudgetSpansCodesAndRecoversAfterTheWindow()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        await RequestCodeAsync(client, TcuEmail);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await VerifyAsync(client, WrongCode(fixture.Email.LastCode));
        }

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        await RequestCodeAsync(client, TcuEmail);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await VerifyAsync(client, WrongCode(fixture.Email.LastCode));
        }

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        await RequestCodeAsync(client, TcuEmail);
        using (var tenth = await VerifyAsync(client, WrongCode(fixture.Email.LastCode)))
        {
            Assert.AreEqual("CODE_INVALID", (await ErrorAsync(tenth))?.Code);
        }

        using (var eleventh = await VerifyAsync(client, fixture.Email.LastCode))
        {
            Assert.AreEqual(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
            var error = await ErrorAsync(eleventh);
            Assert.AreEqual("TOO_MANY_REQUESTS", error?.Code);
            Assert.AreEqual(AffiliationFixture.Now + VerificationPolicy.FailureWindow, error?.RetryAt);
        }

        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()));
        fixture.Clock.Advance(VerificationPolicy.FailureWindow);
        await RequestCodeAsync(client, TcuEmail);
        using var after = await VerifyAsync(client, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, after.StatusCode);
    }

    // IDV.md row "5a, what counts as a failure".
    [TestMethod]
    public async Task OnlyAWrongCodeCountsAsAFailure()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        using (var noCode = await VerifyAsync(client, "123456"))
        {
            Assert.AreEqual("CODE_EXPIRED", (await ErrorAsync(noCode))?.Code);
        }

        await RequestCodeAsync(client, TcuEmail);
        foreach (var malformed in new[] { "12a456", "١٢٣٤٥٦", "12345", "1234567", "" })
        {
            using var response = await VerifyAsync(client, malformed);
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var error = await ErrorAsync(response);
            Assert.AreEqual("VALIDATION_FAILED", error?.Code, malformed);
            Assert.IsTrue(error?.Fields?.ContainsKey("code"));
        }

        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
        using (var padded = await VerifyAsync(client, $"  {WrongCode(fixture.Email.LastCode)} "))
        {
            Assert.AreEqual("CODE_INVALID", (await ErrorAsync(padded))?.Code, "surrounding whitespace is trimmed");
        }

        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.SumAsync(item => item.FailedAttempts)));

        for (var attempt = 0; attempt < 9; attempt++)
        {
            if (attempt % 4 == 0)
            {
                fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
                await RequestCodeAsync(client, TcuEmail);
            }

            await VerifyAsync(client, WrongCode(fixture.Email.LastCode));
        }

        var counted = await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync());
        using (var limited = await VerifyAsync(client, WrongCode(fixture.Email.LastCode)))
        {
            Assert.AreEqual(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }

        Assert.AreEqual(counted, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()), "429 counts nothing");
    }

    // IDV.md row "Shopping check" and "Access mapping assumed from Authentication".
    [TestMethod]
    public async Task ShoppingCheckFollowsStepTwoOrder()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var restricted = await fixture.CreateAccountAsync("restricted@example.org",
            VolunteerAuthorization.VolunteerRole, VolunteerAuthorization.VolunteerRestrictedRole);
        await SeedVerificationAsync(fixture, restricted, "R@TCU.EDU");
        var volunteer = await fixture.CreateAccountAsync("volunteer@example.org", VolunteerAuthorization.VolunteerRole);
        var tcu = await fixture.CreateAccountAsync("tcu@example.org");
        await SeedVerificationAsync(fixture, tcu, "T@TCU.EDU");
        var neither = await fixture.CreateAccountAsync("neither@example.org");
        var removed = await fixture.CreateAccountAsync("removed@example.org", VolunteerAuthorization.VolunteerRole);
        await SeedVerificationAsync(fixture, removed, "X@TCU.EDU");
        await fixture.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<AuthAccount>>();
            return await users.RemoveFromRoleAsync((await users.FindByIdAsync(removed.ToString()))!,
                VolunteerAuthorization.VolunteerRole);
        });

        Assert.AreEqual(new ShoppingEligibility(ShoppingEligibilityKind.Restricted), await ShoppingCheckAsync(fixture, restricted));
        Assert.AreEqual(new ShoppingEligibility(ShoppingEligibilityKind.Eligible, EligibilityBasis.VolunteerRole),
            await ShoppingCheckAsync(fixture, volunteer));
        Assert.AreEqual(new ShoppingEligibility(ShoppingEligibilityKind.Eligible, EligibilityBasis.TcuEmail),
            await ShoppingCheckAsync(fixture, tcu));
        Assert.AreEqual(new ShoppingEligibility(ShoppingEligibilityKind.NotEligible), await ShoppingCheckAsync(fixture, neither));
        Assert.AreEqual(new ShoppingEligibility(ShoppingEligibilityKind.Eligible, EligibilityBasis.TcuEmail),
            await ShoppingCheckAsync(fixture, removed), "role-only removal leaves NONE (OI-22)");
        await Assert.ThrowsExactlyAsync<AccessStateUnavailableException>(
            () => ShoppingCheckAsync(fixture, tcu, new Faults.AlwaysUnavailable()));
    }

    // IDV.md row "Revocation, existing session".
    [TestMethod]
    public async Task RestrictionMidSessionTakesEffectOnTheNextCheck()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync("volunteer@example.org", VolunteerAuthorization.VolunteerRole);
        using var client = await fixture.SignedInClientAsync("volunteer@example.org");
        Assert.IsTrue((await EligibilityAsync(client)).Eligible);

        await fixture.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<AuthAccount>>();
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await roles.CreateAsync(new IdentityRole<Guid>(VolunteerAuthorization.VolunteerRestrictedRole));
            return await users.AddToRoleAsync((await users.FindByIdAsync(account.ToString()))!,
                VolunteerAuthorization.VolunteerRestrictedRole);
        });

        Assert.AreEqual(ShoppingEligibilityKind.Restricted, (await ShoppingCheckAsync(fixture, account)).Kind);
        var status = await EligibilityAsync(client);
        Assert.IsFalse(status.Eligible);
        Assert.AreEqual(VolunteerAccessValues.Restricted, status.VolunteerAccess);
    }

    // IDV.md row "Cross-account".
    [TestMethod]
    public async Task OneAccountsCodeDoesNotVerifyAnother()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync("a@example.org");
        await fixture.CreateAccountAsync("b@example.org");
        using var a = await fixture.SignedInClientAsync("a@example.org");
        using var b = await fixture.SignedInClientAsync("b@example.org");
        await RequestCodeAsync(a, TcuEmail);
        var code = fixture.Email.LastCode;

        using var stolen = await VerifyAsync(b, code);

        Assert.AreEqual("CODE_EXPIRED", (await ErrorAsync(stolen))?.Code);
        Assert.IsFalse((await EligibilityAsync(b)).TcuEmailVerified);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.SumAsync(item => item.FailedAttempts)));
        using var own = await VerifyAsync(a, code);
        Assert.AreEqual(HttpStatusCode.OK, own.StatusCode);
    }

    // IDV.md row "Already verified".
    [TestMethod]
    [DataRow("other@tcu.edu")]
    [DataRow("someone@example.org")]
    public async Task VerifiedAccountCannotRequestAnotherCode(string address)
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await VerifiedThroughApiAsync(client, fixture, TcuEmail);
        var before = await fixture.WithAuthAsync(db => db.TcuEmailVerifications.AsNoTracking().SingleAsync());
        fixture.Clock.Advance(TimeSpan.FromHours(2));

        using var response = await RequestCodeAsync(client, address);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("ALREADY_VERIFIED", (await ErrorAsync(response))?.Code);
        Assert.AreEqual(1, fixture.Email.Count);
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
        var after = await fixture.WithAuthAsync(db => db.TcuEmailVerifications.AsNoTracking().SingleAsync());
        Assert.AreEqual(before.NormalizedEmail, after.NormalizedEmail);
        Assert.AreEqual(before.VerifiedAt, after.VerifiedAt);
    }

    // IDV.md row "Ownership" (first half; the sign-in email change half waits for that operation to exist).
    [TestMethod]
    public async Task EachAccountCompletesItsOwnVerification()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync("a@example.org");
        await fixture.CreateAccountAsync("b@example.org");
        using var a = await fixture.SignedInClientAsync("a@example.org");
        using var b = await fixture.SignedInClientAsync("b@example.org");
        await VerifiedThroughApiAsync(a, fixture, "x@tcu.edu");

        using var requested = await RequestCodeAsync(b, "x@tcu.edu");
        Assert.AreEqual(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.IsFalse((await EligibilityAsync(b)).TcuEmailVerified, "not verified by the shared address");

        using var verified = await VerifyAsync(b, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, verified.StatusCode);
        Assert.IsTrue((await EligibilityAsync(a)).TcuEmailVerified);
        Assert.IsTrue((await EligibilityAsync(b)).TcuEmailVerified);
    }

    // IDV.md row "Signed out".
    [TestMethod]
    public async Task SignedOutCallsAreRefused()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        using var client = fixture.Host.CreateClient();

        using var get = await client.GetAsync("/api/v1/me/shopping-eligibility");
        using var request = await RequestCodeAsync(client, TcuEmail);
        using var verify = await VerifyAsync(client, "123456");

        foreach (var response in new[] { get, request, verify })
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual("AUTHENTICATION_REQUIRED", (await ErrorAsync(response))?.Code);
        }

        Assert.AreEqual(0, fixture.Email.Count);
    }

    // IDV.md row "Antiforgery".
    [TestMethod]
    public async Task PostsWithoutAValidAntiforgeryTokenAreRefused()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        await fixture.CreateAccountAsync("other@example.org");
        using var client = await fixture.SignedInClientAsync(Shopper);
        using var other = await fixture.SignedInClientAsync("other@example.org");
        var foreignToken = await AffiliationFixture.TokenAsync(other);

        using var noTokenRequest = await RequestCodeAsync(client, TcuEmail, withToken: false);
        using var noTokenVerify = await VerifyAsync(client, "123456", withToken: false);
        using var foreignRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/tcu-email/codes")
        {
            Content = JsonContent.Create(new RequestTcuCodeRequest(TcuEmail, Guid.NewGuid())),
        };
        foreignRequest.Headers.Add("RequestVerificationToken", foreignToken);
        using var foreign = await client.SendAsync(foreignRequest);
        using var foreignVerifyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/tcu-email/verification")
        {
            Content = JsonContent.Create(new VerifyTcuCodeRequest("123456")),
        };
        foreignVerifyRequest.Headers.Add("RequestVerificationToken", foreignToken);
        using var foreignVerify = await client.SendAsync(foreignVerifyRequest);

        foreach (var response in new[] { noTokenRequest, noTokenVerify, foreign, foreignVerify })
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual("ANTIFORGERY_INVALID", (await ErrorAsync(response))?.Code);
        }

        Assert.AreEqual(0, fixture.Email.Count);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me/shopping-eligibility")).StatusCode);
    }

    // IDV.md row "Verified address not exposed", both verification paths.
    [TestMethod]
    public async Task EligibilityNeverContainsTheVerifiedAddress()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var profile = await fixture.SignedInClientAsync(Shopper);
        await VerifiedThroughApiAsync(profile, fixture, TcuEmail);
        var profileBody = await profile.GetStringAsync("/api/v1/me/shopping-eligibility");
        Assert.IsFalse(profileBody.Contains(TcuEmail, StringComparison.OrdinalIgnoreCase));

        using var registered = fixture.Host.CreateClient();
        using var start = await registered.PostAsJsonAsync("/api/v1/auth/registrations",
            new StartRegistrationRequest("new.frog@tcu.edu", AffiliationFixture.Password, Guid.NewGuid()));
        var registration = (await start.Content.ReadFromJsonAsync<ApiResponse<RegistrationChallengeResponse>>())!.Data!;
        using var complete = await registered.PostAsJsonAsync(
            $"/api/v1/auth/registrations/{registration.RegistrationId}/verification",
            new VerifyRegistrationRequest(fixture.Email.LastCode));
        Assert.AreEqual(HttpStatusCode.OK, complete.StatusCode);

        var registeredBody = await registered.GetStringAsync("/api/v1/me/shopping-eligibility");
        StringAssert.Contains(registeredBody, "\"tcuEmailVerified\":true");
        Assert.IsFalse(registeredBody.Contains("new.frog@tcu.edu", StringComparison.OrdinalIgnoreCase));
    }
}
