using System.Globalization;
using Microsoft.AspNetCore.Identity;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Authentication;

// BR-tcu-email-domain and IDV.md "Exact tcu.edu only, with a lenient shape check" (IDV.md test rows 3a, 3a shape,
// 3a culture, One domain check).
[TestClass]
public sealed class TcuEmailAddressTests
{
    private static readonly UpperInvariantLookupNormalizer Normalizer = new();

    [TestMethod]
    [DataRow("a@tcu.edu")]
    [DataRow("A@TCU.EDU")]
    [DataRow("first.last+x@tcu.edu")]
    [DataRow("o'neil@tcu.edu")]
    public void AcceptsExactTcuAddresses(string address) => Assert.IsTrue(TcuEmailAddress.IsAcceptable(address));

    [TestMethod]
    [DataRow("a@frog.tcu.edu")]
    [DataRow("a@tcu.edu.co")]
    [DataRow("a@xtcu.edu")]
    [DataRow("a@b@tcu.edu")]
    [DataRow("@tcu.edu")]
    [DataRow("a b@tcu.edu")]
    [DataRow("a\u0001b@tcu.edu")]
    [DataRow("not-an-address")]
    [DataRow("")]
    public void RefusesEverythingElse(string address) => Assert.IsFalse(TcuEmailAddress.IsAcceptable(address));

    [TestMethod]
    public void RefusesAddressesLongerThan254Characters()
    {
        var limit = new string('a', 254 - "@tcu.edu".Length) + "@tcu.edu";
        Assert.IsTrue(TcuEmailAddress.IsAcceptable(limit));
        Assert.IsFalse(TcuEmailAddress.IsAcceptable("a" + limit));
    }

    [TestMethod]
    public void SameAddressInAnyCaseNormalizesToOneValue() =>
        Assert.AreEqual(Normalizer.NormalizeEmail("a@tcu.edu"), Normalizer.NormalizeEmail("A@TCU.EDU"));

    [TestMethod]
    public void TurkishCultureDoesNotChangeTheCheckOrTheNormalizedEmail()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.IsTrue(TcuEmailAddress.IsAcceptable("KIM@TCU.EDU"));
            Assert.AreEqual(Normalizer.NormalizeEmail("kim@tcu.edu"), Normalizer.NormalizeEmail("KIM@TCU.EDU"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // One domain check: registration completion uses HasExactTcuDomain on the normalized email, and the affiliation
    // request uses IsAcceptable, which applies the same domain check.
    [TestMethod]
    [DataRow("a@b@tcu.edu", false)]
    [DataRow("a@frog.tcu.edu", false)]
    [DataRow("a@tcu.edu.co", false)]
    [DataRow("A@TCU.EDU", true)]
    public void RegistrationAndAffiliationAgree(string address, bool tcu)
    {
        Assert.AreEqual(tcu, TcuEmailAddress.HasExactTcuDomain(Normalizer.NormalizeEmail(address)));
        Assert.AreEqual(tcu, TcuEmailAddress.IsAcceptable(address));
    }
}
