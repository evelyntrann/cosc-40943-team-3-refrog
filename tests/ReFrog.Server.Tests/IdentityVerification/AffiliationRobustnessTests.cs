using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Tests.Support;
using static ReFrog.Server.Tests.IdentityVerification.AffiliationApi;

namespace ReFrog.Server.Tests.IdentityVerification;

// UC-IDV-verify-affiliation robustness rows from IDV.md Tests: concurrency, idempotent sends, delivery and save
// failures, request limits, and logging (ROB-external-service-failure, FR-SAVE-confirmed-success, §8.2.7, §8.2.8).
[TestClass]
public sealed class AffiliationRobustnessTests
{
    private const string Shopper = "shopper@example.org";
    private const string TcuEmail = "kim.frog@tcu.edu";

    // IDV.md row "Concurrent verification".
    [TestMethod]
    public async Task ConcurrentCorrectSubmissionsVerifyOnce()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var first = await fixture.SignedInClientAsync(Shopper);
        using var second = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(first, TcuEmail);
        var code = fixture.Email.LastCode;

        var responses = await Task.WhenAll(VerifyAsync(first, code), VerifyAsync(second, code));

        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync(item => item.ConsumedAt != null)));
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    // IDV.md row "Concurrent requests".
    [TestMethod]
    public async Task ConcurrentRequestsFromOneAccountSendOneCode()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var first = await fixture.SignedInClientAsync(Shopper);
        using var second = await fixture.SignedInClientAsync(Shopper);

        var responses = await Task.WhenAll(RequestCodeAsync(first, TcuEmail), RequestCodeAsync(second, TcuEmail));

        CollectionAssert.AreEquivalent(new[] { HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests },
            responses.Select(response => response.StatusCode).ToArray());
        Assert.AreEqual(1, fixture.Email.Count);
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges
            .CountAsync(item => item.ConsumedAt == null && item.SupersededAt == null)));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    // IDV.md row "Concurrent requests to one email".
    [TestMethod]
    public async Task ConcurrentRequestsToOneEmailRespectTheDestinationLimit()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync("earlier@example.org");
        await fixture.CreateAccountAsync("a@example.org");
        await fixture.CreateAccountAsync("b@example.org");
        using var earlier = await fixture.SignedInClientAsync("earlier@example.org");
        for (var index = 0; index < 4; index++)
        {
            using var sent = await RequestCodeAsync(earlier, "x@tcu.edu");
            Assert.AreEqual(HttpStatusCode.Accepted, sent.StatusCode);
            fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        }

        using var a = await fixture.SignedInClientAsync("a@example.org");
        using var b = await fixture.SignedInClientAsync("b@example.org");
        var responses = await Task.WhenAll(RequestCodeAsync(a, "x@tcu.edu"), RequestCodeAsync(b, "x@tcu.edu"));

        CollectionAssert.AreEquivalent(new[] { HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests },
            responses.Select(response => response.StatusCode).ToArray());
        Assert.AreEqual(5, fixture.Email.Count);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    // IDV.md row "Duplicate send action".
    [TestMethod]
    public async Task SameRequestIdSendsOnceAndRejectsAChangedEmail()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        var requestId = Guid.NewGuid();

        using var first = await RequestCodeAsync(client, TcuEmail, requestId);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        using var again = await RequestCodeAsync(client, TcuEmail, requestId);
        using var changed = await RequestCodeAsync(client, "other@tcu.edu", requestId);

        Assert.AreEqual(HttpStatusCode.Accepted, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, again.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ApiResponse<TcuCodeResponse>>())!.Data!;
        var againBody = (await again.Content.ReadFromJsonAsync<ApiResponse<TcuCodeResponse>>())!.Data!;
        Assert.AreEqual(firstBody.ExpiresAt, againBody.ExpiresAt);
        Assert.AreEqual(firstBody.ResendAvailableAt, againBody.ResendAvailableAt);
        Assert.AreEqual(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.AreEqual("REQUEST_ID_REUSED", (await ErrorAsync(changed))?.Code);
        Assert.AreEqual(1, fixture.Email.Count);
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
    }

    // IDV.md row "Delivery failure", thrown and refused.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DeliveryFailureKeepsTheNewCodeLiveAndRetriesWithoutSending(bool throws)
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);
        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        fixture.Email.ThrowOnSend = throws;
        fixture.Email.Outcome = throws ? EmailDeliveryOutcome.Accepted : EmailDeliveryOutcome.Failed;
        var requestId = Guid.NewGuid();

        using var failed = await RequestCodeAsync(client, TcuEmail, requestId);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.AreEqual("EMAIL_NOT_SENT", (await ErrorAsync(failed))?.Code);
        var challenges = await fixture.WithAuthAsync(db => db.VerificationChallenges.AsNoTracking()
            .OrderBy(item => item.CreatedAt).ToListAsync());
        Assert.HasCount(2, challenges);
        Assert.IsNotNull(challenges[0].SupersededAt, "previous code superseded");
        Assert.IsNull(challenges[1].SupersededAt, "new code live");
        Assert.IsNull(challenges[1].ConsumedAt);

        using var retry = await RequestCodeAsync(client, TcuEmail, requestId);
        Assert.AreEqual("EMAIL_NOT_SENT", (await ErrorAsync(retry))?.Code);
        Assert.AreEqual(2, fixture.Email.Count, "the retry sends nothing");
    }

    // IDV.md row "Retry while the send is in flight".
    [TestMethod]
    public async Task RetryWhileTheFirstSendIsInFlightIsNotReportedAsSent()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        using var retryClient = await fixture.SignedInClientAsync(Shopper);
        var requestId = Guid.NewGuid();
        fixture.Email.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = RequestCodeAsync(client, TcuEmail, requestId);
        await fixture.Email.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        using (var retry = await RequestCodeAsync(retryClient, TcuEmail, requestId))
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, retry.StatusCode);
            Assert.AreEqual("EMAIL_NOT_SENT", (await ErrorAsync(retry))?.Code);
        }

        Assert.AreEqual(1, fixture.Email.Count);
        fixture.Email.Gate.SetResult();
        using var firstResponse = await first;
        Assert.AreEqual(HttpStatusCode.Accepted, firstResponse.StatusCode);
    }

    // IDV.md rows "Dropped connection during send" and the empty-outcome half of "Retry while the send is in flight".
    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task DroppedConnectionLeavesAnUnconfirmedCodeThatBlocksOnlyUntilTheCooldown()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        var requestId = Guid.NewGuid();
        fixture.Email.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var abort = new CancellationTokenSource();

        var dropped = RequestCodeAsync(client, TcuEmail, requestId, cancellationToken: abort.Token);
        await fixture.Email.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await abort.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => dropped);
        fixture.Email.Gate = null;

        using (var retry = await RequestCodeAsync(client, TcuEmail, requestId))
        {
            Assert.AreEqual("EMAIL_NOT_SENT", (await ErrorAsync(retry))?.Code);
        }

        Assert.AreEqual(1, fixture.Email.Count);
        using (var early = await RequestCodeAsync(client, TcuEmail))
        {
            Assert.AreEqual(HttpStatusCode.TooManyRequests, early.StatusCode);
        }

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        using var later = await RequestCodeAsync(client, TcuEmail);
        Assert.AreEqual(HttpStatusCode.Accepted, later.StatusCode);
    }

    // IDV.md rows "Save failure" and "Failure mapping".
    [TestMethod]
    public async Task FailedCommitsAreNotRecordedAndChangeNothing()
    {
        var faults = new Faults();
        await using var fixture = await AffiliationFixture.CreateAsync(faults.Install);
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);
        var code = fixture.Email.LastCode;
        var liveExpiry = (await EligibilityAsync(client)).PendingCodeExpiresAt;

        faults.CommitFails = true;
        using (var verify = await VerifyAsync(client, code))
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, verify.StatusCode);
            Assert.AreEqual("NOT_RECORDED", (await ErrorAsync(verify))?.Code);
        }

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        using (var request = await RequestCodeAsync(client, TcuEmail))
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, request.StatusCode);
            Assert.AreEqual("NOT_RECORDED", (await ErrorAsync(request))?.Code);
        }

        faults.CommitFails = false;
        Assert.AreEqual(1, fixture.Email.Count, "no email for an uncommitted code");
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()));
        Assert.AreEqual(liveExpiry, (await EligibilityAsync(client)).PendingCodeExpiresAt, "previous code still live");

        faults.ChallengeReadsFail = true;
        using (var request = await RequestCodeAsync(client, TcuEmail))
        {
            Assert.AreEqual("ACCESS_UNAVAILABLE", (await ErrorAsync(request))?.Code, "failure before the first write");
        }

        faults.ChallengeReadsFail = false;
        using var recovered = await VerifyAsync(client, code);
        Assert.AreEqual(HttpStatusCode.OK, recovered.StatusCode, "the code was never consumed");
    }

    // IDV.md row "Outcome not recorded".
    [TestMethod]
    public async Task AcceptedSendWhoseOutcomeCannotBeRecordedIsNotConfirmedButStillVerifies()
    {
        var faults = new Faults();
        await using var fixture = await AffiliationFixture.CreateAsync(faults.Install);
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        faults.SendOutcomeWriteFails = true;
        using var request = await RequestCodeAsync(client, TcuEmail);
        faults.SendOutcomeWriteFails = false;

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, request.StatusCode);
        Assert.AreEqual("EMAIL_NOT_SENT", (await ErrorAsync(request))?.Code);
        using var verify = await VerifyAsync(client, fixture.Email.LastCode);
        Assert.AreEqual(HttpStatusCode.OK, verify.StatusCode);
    }

    // IDV.md row "Request limits".
    [TestMethod]
    public async Task RequestsAreSpacedAndLimitedPerAccountAndPerEmail()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        await RequestCodeAsync(client, TcuEmail);
        fixture.Clock.Advance(VerificationPolicy.ResendSpacing - TimeSpan.FromSeconds(1));
        using (var early = await RequestCodeAsync(client, TcuEmail))
        {
            Assert.AreEqual(HttpStatusCode.TooManyRequests, early.StatusCode);
            Assert.AreEqual(AffiliationFixture.Now + VerificationPolicy.ResendSpacing, (await ErrorAsync(early))?.RetryAt);
        }

        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        for (var index = 1; index < 5; index++)
        {
            using var allowed = await RequestCodeAsync(client, $"kim{index}@tcu.edu");
            Assert.AreEqual(HttpStatusCode.Accepted, allowed.StatusCode, $"request {index + 1}");
            fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        }

        using (var sixth = await RequestCodeAsync(client, "kim6@tcu.edu"))
        {
            Assert.AreEqual(HttpStatusCode.TooManyRequests, sixth.StatusCode, "per account");
            Assert.AreEqual(AffiliationFixture.Now + VerificationPolicy.RequestWindow, (await ErrorAsync(sixth))?.RetryAt);
        }

        await fixture.CreateAccountAsync("a@example.org");
        await fixture.CreateAccountAsync("b@example.org");
        using var a = await fixture.SignedInClientAsync("a@example.org");
        using var b = await fixture.SignedInClientAsync("b@example.org");
        var start = fixture.Clock.GetUtcNow();
        for (var index = 0; index < 5; index++)
        {
            using var sent = await RequestCodeAsync(index % 2 == 0 ? a : b, "shared@tcu.edu");
            Assert.AreEqual(HttpStatusCode.Accepted, sent.StatusCode);
            fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        }

        using var perEmail = await RequestCodeAsync(b, "shared@tcu.edu");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, perEmail.StatusCode, "per destination email");
        Assert.AreEqual(start + VerificationPolicy.RequestWindow, (await ErrorAsync(perEmail))?.RetryAt);
    }

    // IDV.md row "Key rotation", retired-key half at the endpoint.
    [TestMethod]
    public async Task RetiredKeyAnswersNotRecordedAtTheEndpoint()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);
        await RequestCodeAsync(client, TcuEmail);
        await fixture.WithAuthAsync(db => db.VerificationChallenges
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.KeyVersion, 9)));

        using var response = await VerifyAsync(client, fixture.Email.LastCode);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual("NOT_RECORDED", (await ErrorAsync(response))?.Code);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
    }

    // IDV.md row "Logging".
    [TestMethod]
    public async Task LogsCarryNoCodeHmacKeyOrEmail()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        await fixture.CreateAccountAsync(Shopper);
        using var client = await fixture.SignedInClientAsync(Shopper);

        await RequestCodeAsync(client, TcuEmail);
        var first = fixture.Email.LastCode;
        await VerifyAsync(client, WrongCode(first));
        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        fixture.Email.ThrowOnSend = true;
        await RequestCodeAsync(client, TcuEmail);
        var second = fixture.Email.LastCode;
        fixture.Email.ThrowOnSend = false;
        await VerifyAsync(client, second);

        var hmacs = await fixture.WithAuthAsync(db => db.VerificationChallenges.AsNoTracking()
            .Select(item => item.CodeHmac).ToListAsync());
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(index => (byte)index).ToArray());
        var secrets = new[] { first, second, TcuEmail, key }
            .Concat(hmacs.Select(Convert.ToBase64String))
            .Concat(hmacs.Select(Convert.ToHexString));
        var logs = fixture.Logs.Messages.ToList();
        foreach (var secret in secrets)
        {
            Assert.IsFalse(logs.Any(line => line.Contains(secret, StringComparison.OrdinalIgnoreCase)), $"log leaked {secret.Length}-char value");
        }

        Assert.IsTrue(fixture.Logs.Entries.Any(entry => entry.Level == LogLevel.Error &&
            entry.Text.Contains("AffiliationCodeSendNotConfirmed", StringComparison.Ordinal)));
    }
}
