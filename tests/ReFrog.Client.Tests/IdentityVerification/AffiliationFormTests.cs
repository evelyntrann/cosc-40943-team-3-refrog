using System.Net;
using ReFrog.Client.Features.IdentityVerification;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;

namespace ReFrog.Client.Tests.IdentityVerification;

// AffiliationForm behavior the IDV.md component description fixes: request-id reuse, resend timing (5a1),
// unknown outcomes (FR-SAVE-unknown-outcome), and never keeping a stale status (2d1, §8.2.1).
[TestClass]
public sealed class AffiliationFormTests
{
    private static readonly DateTimeOffset Now = new(2027, 4, 26, 15, 0, 0, TimeSpan.Zero);
    private static readonly TcuCodeResponse Sent = new(Now.AddMinutes(10), Now.AddMinutes(1));

    [TestMethod]
    public async Task RetryAfterADroppedResponseReusesTheRequestId()
    {
        var api = new ScriptedApi().Drop(ScriptedApi.Codes)
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(Sent));
        var form = new AffiliationForm(api.Client(), new FixedClock(Now)) { Email = "kim@tcu.edu" };

        await form.RequestCodeAsync();
        Assert.AreEqual("NETWORK_UNAVAILABLE", form.Error?.Code);
        await form.RequestCodeAsync();

        Assert.AreEqual(api.CodeRequests[0].RequestId, api.CodeRequests[1].RequestId);
        Assert.AreEqual(Sent, form.Pending);
    }

    [TestMethod]
    public async Task EachAnsweredSendIsFollowedByANewRequestId()
    {
        var clock = new FixedClock(Now);
        var api = new ScriptedApi()
            .Fail(ScriptedApi.Codes, HttpStatusCode.ServiceUnavailable, "EMAIL_NOT_SENT")
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(Sent))
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(Sent));
        var form = new AffiliationForm(api.Client(), clock) { Email = "kim@tcu.edu" };

        await form.RequestCodeAsync();
        await form.RequestCodeAsync();
        clock.Now = Sent.ResendAvailableAt;
        await form.RequestCodeAsync();

        Assert.HasCount(3, api.CodeRequests.Select(request => request.RequestId).Distinct().ToList());
    }

    [TestMethod]
    public async Task ResendInTheSameVisitPostsTheRememberedAddress()
    {
        var clock = new FixedClock(Now);
        var api = new ScriptedApi()
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(Sent))
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(Sent));
        var form = new AffiliationForm(api.Client(), clock) { Email = "kim@tcu.edu" };
        await form.RequestCodeAsync();

        clock.Now = Sent.ResendAvailableAt;
        await form.RequestCodeAsync();

        Assert.HasCount(2, api.CodeRequests);
        Assert.AreEqual("kim@tcu.edu", api.CodeRequests[1].Email);
        Assert.IsFalse(form.ChangingEmail);
    }

    [TestMethod]
    public async Task ResendWaitsForTheServersResendTime()
    {
        var clock = new FixedClock(Now);
        var api = new ScriptedApi().Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(Sent));
        var form = new AffiliationForm(api.Client(), clock) { Email = "kim@tcu.edu" };
        await form.RequestCodeAsync();

        clock.Now = Sent.ResendAvailableAt.AddSeconds(-1);
        Assert.IsFalse(form.CanResend);
        await form.RequestCodeAsync();
        Assert.HasCount(1, api.CodeRequests, "no request is sent early");

        clock.Now = Sent.ResendAvailableAt;
        Assert.IsTrue(form.CanResend);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.ServiceUnavailable, "NOT_RECORDED")]
    [DataRow(HttpStatusCode.BadGateway, null)]
    [DataRow((HttpStatusCode)0, null)]
    public async Task UnknownVerificationOutcomeReReadsTheStatus(HttpStatusCode status, string? code)
    {
        var api = new ScriptedApi();
        if (status == 0)
        {
            api.Drop(ScriptedApi.Verification);
        }
        else if (code is null)
        {
            api.Raw(ScriptedApi.Verification, status, "<html>gateway</html>");
        }
        else
        {
            api.Fail(ScriptedApi.Verification, status, code);
        }

        api.Ok(ScriptedApi.Status(true, EligibilityBasis.TcuEmail, true));
        var form = new AffiliationForm(api.Client(), new FixedClock(Now)) { Code = "123456" };

        await form.SubmitCodeAsync();

        Assert.AreEqual(1, api.EligibilityReads);
        Assert.IsTrue(form.ShowsTcuVerified);
    }

    [TestMethod]
    public async Task AFailedReadDropsTheEarlierStatus()
    {
        var api = new ScriptedApi().Ok(ScriptedApi.Status(true, EligibilityBasis.TcuEmail, true))
            .Fail(ScriptedApi.Eligibility, HttpStatusCode.ServiceUnavailable, "ACCESS_UNAVAILABLE");
        var form = new AffiliationForm(api.Client(), new FixedClock(Now));

        await form.LoadStatusAsync();
        Assert.IsTrue(form.ShowsTcuVerified);
        await form.LoadStatusAsync();

        Assert.IsTrue(form.StatusUnavailable);
        Assert.IsNull(form.Status);
        Assert.IsFalse(form.ShowsTcuVerified);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
