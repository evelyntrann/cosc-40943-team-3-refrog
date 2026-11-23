using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Client.Features.IdentityVerification;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;

namespace ReFrog.Client.Tests.IdentityVerification;

// UC-IDV-verify-affiliation front-end rows from IDV.md Tests: "2b and 2c, front end", "2d, front end",
// "3a, front end", "5b", and the profile half of "Verified address not exposed".
[TestClass]
public sealed class AffiliationSectionTests
{
    private const string TcuVerified = "TCU email verified";
    private const string Inactive = "Volunteer access inactive—administrator review required";
    private const string Unavailable = "Unable to verify access—try again";

    [TestMethod]
    public void VolunteerRoleOnlyShowsVolunteerAndNeverTcuVerified()
    {
        using var context = new BunitContext();
        var cut = Render(context, new ScriptedApi().Ok(ScriptedApi.Status(true, EligibilityBasis.VolunteerRole, false,
            VolunteerAccessValues.RoleActive)));

        cut.WaitForAssertion(() => Assert.AreEqual("Volunteer", cut.Find("[data-testid=volunteer]").TextContent));
        Assert.DoesNotContain(TcuVerified, cut.Markup);
        Assert.IsEmpty(cut.FindAll("#affiliation-email"));
    }

    [TestMethod]
    public void VolunteerWithVerifiedEmailShowsBothAsSeparateFacts()
    {
        using var context = new BunitContext();
        var cut = Render(context, new ScriptedApi().Ok(ScriptedApi.Status(true, EligibilityBasis.VolunteerRole, true,
            VolunteerAccessValues.RoleActive)));

        cut.WaitForAssertion(() => Assert.AreEqual(TcuVerified, cut.Find("[data-testid=tcu-verified]").TextContent));
        Assert.AreEqual("Volunteer", cut.Find("[data-testid=volunteer]").TextContent);
    }

    [TestMethod]
    public void RestrictedAccountShowsTheInactiveMessageEvenWhenEmailIsVerified()
    {
        using var context = new BunitContext();
        var cut = Render(context, new ScriptedApi().Ok(ScriptedApi.Status(false, null, true, VolunteerAccessValues.Restricted)));

        cut.WaitForAssertion(() => Assert.AreEqual(Inactive, cut.Find("[data-testid=restricted]").TextContent));
        Assert.DoesNotContain(TcuVerified, cut.Markup);
        Assert.IsEmpty(cut.FindAll("#affiliation-email"));
        Assert.IsEmpty(cut.FindAll("[data-testid=volunteer]"));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.ServiceUnavailable, "ACCESS_UNAVAILABLE")]
    [DataRow(HttpStatusCode.ServiceUnavailable, "AUTHENTICATION_UNAVAILABLE")]
    [DataRow(HttpStatusCode.InternalServerError, "UNEXPECTED_ERROR")]
    [DataRow(HttpStatusCode.BadGateway, null)]
    [DataRow((HttpStatusCode)0, null)]
    public void UnreadableStatusOffersRetryAndShowsNoStatus(HttpStatusCode status, string? code)
    {
        var api = new ScriptedApi();
        if (status == 0)
        {
            api.Drop(ScriptedApi.Eligibility);
        }
        else if (code is null)
        {
            api.Raw(ScriptedApi.Eligibility, status, "<html>gateway</html>");
        }
        else
        {
            api.Fail(ScriptedApi.Eligibility, status, code);
        }

        api.Ok(ScriptedApi.Status(true, EligibilityBasis.TcuEmail, true));
        using var context = new BunitContext();
        var cut = Render(context, api);

        cut.WaitForAssertion(() => Assert.AreEqual(Unavailable, cut.Find("[data-testid=access-unavailable]").TextContent));
        Assert.DoesNotContain(TcuVerified, cut.Markup);
        Assert.IsEmpty(cut.FindAll("#affiliation-email"));

        cut.Find("button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual(TcuVerified, cut.Find("[data-testid=tcu-verified]").TextContent));
        Assert.IsEmpty(cut.FindAll("[data-testid=access-unavailable]"));
    }

    [TestMethod]
    public void EmailFieldShowsGuidanceAndAServerRefusalKeepsTheTypedAddress()
    {
        var api = new ScriptedApi().Ok(ScriptedApi.Status())
            .Fail(ScriptedApi.Codes, HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED",
                new Dictionary<string, string> { ["email"] = "Enter a TCU email address ending in @tcu.edu." });
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-email"));
        StringAssert.Contains(cut.Find("#affiliation-email-guidance").TextContent,
            "Please use a tcu.edu email if you are a TCU affiliate.");

        cut.Find("#affiliation-email").Change("person@example.org");
        cut.Find("#affiliation-email-form").Submit();

        cut.WaitForAssertion(() => Assert.AreEqual("Enter a TCU email address ending in @tcu.edu.",
            cut.Find("[data-testid=email-error]").TextContent));
        Assert.AreEqual("person@example.org", cut.Find("#affiliation-email").GetAttribute("value"));
        Assert.AreEqual("person@example.org", api.CodeRequests.Single().Email, "the server decides, not the client");
    }

    [TestMethod]
    public void NotEligibleScreenExplainsBothQualifyingPathsAndOffersNoOtherGrant()
    {
        using var context = new BunitContext();
        var cut = Render(context, new ScriptedApi().Ok(ScriptedApi.Status()));

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Find("[data-testid=qualifying-paths]").TextContent,
            "verified TCU email or a current approved volunteer role"));
        Assert.AreEqual("Send code", cut.Find("button").TextContent.Trim());
        Assert.HasCount(1, cut.FindAll("button"), "no override or manual-grant control (OI-19)");
    }

    [TestMethod]
    public void CompletedVerificationShowsTheConfirmationWithoutTheAddress()
    {
        var expires = new DateTimeOffset(2027, 4, 26, 15, 10, 0, TimeSpan.Zero);
        var api = new ScriptedApi().Ok(ScriptedApi.Status())
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(new TcuCodeResponse(expires, expires.AddMinutes(-9))))
            .Reply(ScriptedApi.Verification, HttpStatusCode.OK, ApiResponse.Success(new TcuVerificationResponse(
                true, EligibilityBasis.TcuEmail, true, VolunteerAccessValues.None, null, expires.AddMinutes(-8))));
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-email"));

        cut.Find("#affiliation-email").Change("kim.frog@tcu.edu");
        cut.Find("#affiliation-email-form").Submit();
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));
        StringAssert.Contains(cut.Markup, "10:10 a.m.", "expiry shown in America/Chicago on a 12-hour clock");
        cut.Find("#affiliation-code").Change("012345");
        cut.Find("#affiliation-code-form").Submit();

        cut.WaitForAssertion(() => Assert.AreEqual(TcuVerified, cut.Find("[data-testid=tcu-verified]").TextContent));
        Assert.AreEqual("012345", api.CodeSubmissions.Single().Code, "leading zero kept");
        Assert.DoesNotContain("kim.frog@tcu.edu", cut.Markup);
    }

    [TestMethod]
    public void RejectedCodeOffersResend()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var api = new ScriptedApi().Ok(ScriptedApi.Status(pending: expires))
            .Fail(ScriptedApi.Verification, HttpStatusCode.UnprocessableEntity, "CODE_INVALID");
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));

        cut.Find("#affiliation-code").Change("000000");
        cut.Find("#affiliation-code-form").Submit();

        cut.WaitForAssertion(() => Assert.AreEqual("message for CODE_INVALID", cut.Find("[data-testid=affiliation-error]").TextContent));
        var resend = cut.Find("[data-testid=send-code]");
        Assert.AreEqual("Send a new code", resend.TextContent.Trim());
        Assert.IsFalse(resend.HasAttribute("disabled"));
    }

    // Code review finding 1 on PR #149: after a reload the address is unknown, so the email box is shown
    // beside the live code's box and the shopper enters the address to send a new code.
    [TestMethod]
    public void AfterAReloadTheEmailBoxIsShownBesideTheLiveCode()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(9);
        var api = new ScriptedApi().Ok(ScriptedApi.Status(pending: expires))
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted,
                ApiResponse.Success(new TcuCodeResponse(expires.AddMinutes(1), DateTimeOffset.UtcNow.AddMinutes(1))));
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));
        Assert.AreEqual(string.Empty, cut.Find("#affiliation-email").GetAttribute("value") ?? string.Empty);
        Assert.AreEqual("Send a new code", cut.Find("[data-testid=send-code]").TextContent.Trim());

        cut.Find("#affiliation-email").Change("kim.frog@tcu.edu");
        cut.Find("#affiliation-email-form").Submit();

        cut.WaitForAssertion(() => Assert.HasCount(1, api.CodeRequests));
        Assert.AreEqual("kim.frog@tcu.edu", api.CodeRequests.Single().Email);
        Assert.IsNotNull(cut.Find("#affiliation-code"));
    }

    // Code review finding 2 on PR #149: a mistyped address can be corrected after a code was sent.
    [TestMethod]
    public void AMistypedAddressCanBeCorrectedAndSentAgain()
    {
        var sent = new TcuCodeResponse(DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow.AddSeconds(-1));
        var api = new ScriptedApi().Ok(ScriptedApi.Status())
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(sent))
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(sent));
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-email"));

        cut.Find("#affiliation-email").Change("typo@tcu.edu");
        cut.Find("#affiliation-email-form").Submit();
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));
        Assert.AreEqual("typo@tcu.edu", cut.Find("#affiliation-email").GetAttribute("value"), "the email box stays, with what was typed");

        cut.Find("#affiliation-email").Change("kim@tcu.edu");
        cut.Find("#affiliation-email-form").Submit();

        cut.WaitForAssertion(() => Assert.HasCount(2, api.CodeRequests));
        Assert.AreEqual("kim@tcu.edu", api.CodeRequests[1].Email);
        Assert.AreNotEqual(api.CodeRequests[0].RequestId, api.CodeRequests[1].RequestId);
    }

    // Code review finding 3 on PR #149: the button re-enables itself when the resend time passes.
    // The clock is advanced by hand, so the test does not depend on real time.
    [TestMethod]
    public void SendANewCodeEnablesItselfWhenTheResendTimeArrives()
    {
        var clock = new ManualTimeProvider(ClockStart);
        var sent = new TcuCodeResponse(ClockStart.AddMinutes(10), ClockStart.AddSeconds(60));
        var api = new ScriptedApi().Ok(ScriptedApi.Status())
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted, ApiResponse.Success(sent));
        using var context = new BunitContext();
        var cut = Render(context, api, clock);
        cut.WaitForAssertion(() => cut.Find("#affiliation-email"));
        cut.Find("#affiliation-email").Change("kim@tcu.edu");
        cut.Find("#affiliation-email-form").Submit();
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));
        Assert.IsTrue(cut.Find("[data-testid=send-code]").HasAttribute("disabled"));

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.IsTrue(cut.Find("[data-testid=send-code]").HasAttribute("disabled"), "not before the resend time");
        clock.Advance(TimeSpan.FromSeconds(1));

        cut.WaitForAssertion(() => Assert.IsFalse(cut.Find("[data-testid=send-code]").HasAttribute("disabled")));
        Assert.DoesNotContain("You can send a new code at", cut.Markup);
    }

    // Code review finding 3 on PR #149: an expired code's field goes away without interaction.
    [TestMethod]
    public void TheCodeFieldDisappearsWhenTheCodeExpires()
    {
        var clock = new ManualTimeProvider(ClockStart);
        var api = new ScriptedApi().Ok(ScriptedApi.Status(pending: ClockStart.AddMinutes(5)));
        using var context = new BunitContext();
        var cut = Render(context, api, clock);
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));

        clock.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() => Assert.IsEmpty(cut.FindAll("#affiliation-code")));
        Assert.AreEqual("Send code", cut.Find("[data-testid=send-code]").TextContent.Trim());
    }

    private static readonly DateTimeOffset ClockStart = new(2027, 4, 26, 15, 0, 0, TimeSpan.Zero);

    // Code review finding 4 on PR #149: a 429 shows when the shopper can try again, in Chicago time.
    [TestMethod]
    public void TooManyCodeRequestsShowsTheRetryTime()
    {
        var api = new ScriptedApi().Ok(ScriptedApi.Status())
            .Reply(ScriptedApi.Codes, HttpStatusCode.TooManyRequests, Limited(RetryAt));
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-email"));

        cut.Find("#affiliation-email").Change("kim@tcu.edu");
        cut.Find("#affiliation-email-form").Submit();

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Find("[data-testid=retry-at]").TextContent, "11:00 a.m."));
    }

    [TestMethod]
    public void TooManyWrongCodesShowsTheRetryTime()
    {
        var api = new ScriptedApi().Ok(ScriptedApi.Status(pending: DateTimeOffset.UtcNow.AddMinutes(5)))
            .Reply(ScriptedApi.Verification, HttpStatusCode.TooManyRequests, Limited(RetryAt));
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));

        cut.Find("#affiliation-code").Change("123456");
        cut.Find("#affiliation-code-form").Submit();

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Find("[data-testid=retry-at]").TextContent, "11:00 a.m."));
    }

    [TestMethod]
    public void ErrorsWithoutARetryTimeShowNoTimeLine()
    {
        var api = new ScriptedApi().Ok(ScriptedApi.Status(pending: DateTimeOffset.UtcNow.AddMinutes(5)))
            .Fail(ScriptedApi.Verification, HttpStatusCode.UnprocessableEntity, "CODE_INVALID");
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));

        cut.Find("#affiliation-code").Change("123456");
        cut.Find("#affiliation-code-form").Submit();

        cut.WaitForAssertion(() => cut.Find("[data-testid=affiliation-error]"));
        Assert.IsEmpty(cut.FindAll("[data-testid=retry-at]"));
    }

    // 4:00 p.m. UTC on April 26, 2027 is 11:00 a.m. in Chicago (CDT).
    private static readonly DateTimeOffset RetryAt = new(2027, 4, 26, 16, 0, 0, TimeSpan.Zero);

    private static ApiResponse<object> Limited(DateTimeOffset retryAt) => ApiResponse.Failure<object>(new ApiError(
        ApiErrorCategory.Validation, "TOO_MANY_REQUESTS", "Try again after the stated time.", RetryAt: retryAt));

    private static IRenderedComponent<AffiliationSection> Render(BunitContext context, ScriptedApi api,
        TimeProvider? clock = null)
    {
        context.Services.AddSingleton(api.Client());
        context.Services.AddSingleton(clock ?? TimeProvider.System);
        return context.Render<AffiliationSection>();
    }
}
