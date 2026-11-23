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
        cut.Find("form").Submit();

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
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));
        StringAssert.Contains(cut.Markup, "10:10 a.m.", "expiry shown in America/Chicago on a 12-hour clock");
        cut.Find("#affiliation-code").Change("012345");
        cut.Find("form").Submit();

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
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.AreEqual("message for CODE_INVALID", cut.Find("[data-testid=affiliation-error]").TextContent));
        Assert.IsFalse(cut.Find("[data-testid=resend]").HasAttribute("disabled"));
    }

    // Code review finding 1 on PR #149: after a reload the address is unknown, so resend asks for it.
    [TestMethod]
    public void ResendAfterAReloadAsksForTheAddressInsteadOfPostingABlankOne()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(9);
        var api = new ScriptedApi().Ok(ScriptedApi.Status(pending: expires))
            .Reply(ScriptedApi.Codes, HttpStatusCode.Accepted,
                ApiResponse.Success(new TcuCodeResponse(expires.AddMinutes(1), DateTimeOffset.UtcNow.AddMinutes(1))));
        using var context = new BunitContext();
        var cut = Render(context, api);
        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));

        cut.Find("[data-testid=resend]").Click();

        cut.WaitForAssertion(() => cut.Find("#affiliation-email"));
        Assert.IsEmpty(api.CodeRequests, "no blank address is posted");
        Assert.IsEmpty(cut.FindAll("#affiliation-code"));

        cut.Find("#affiliation-email").Change("kim.frog@tcu.edu");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => cut.Find("#affiliation-code"));
        Assert.AreEqual("kim.frog@tcu.edu", api.CodeRequests.Single().Email);
    }

    private static IRenderedComponent<AffiliationSection> Render(BunitContext context, ScriptedApi api)
    {
        context.Services.AddSingleton(api.Client());
        return context.Render<AffiliationSection>();
    }
}
