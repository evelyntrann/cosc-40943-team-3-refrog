using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class RegistrationCodeTests
{
    [TestMethod]
    public void GeneratedCodeHasSixDigitsIncludingLeadingZeros()
    {
        var codes = new RegistrationCode(new byte[32], 1);

        for (var index = 0; index < 100; index++)
        {
            var code = codes.Generate();
            Assert.AreEqual(6, code.Length);
            Assert.IsTrue(code.All(char.IsAsciiDigit));
        }
    }

    [TestMethod]
    public void HashIsBoundToRegistrationAndChallenge()
    {
        var codes = new RegistrationCode(new byte[32], 1);
        var owner = Guid.NewGuid();
        var challenge = Guid.NewGuid();
        var digest = codes.Hash(owner, challenge, "012345");

        Assert.IsTrue(codes.Matches(1, owner, challenge, "012345", digest));
        Assert.IsFalse(codes.Matches(1, Guid.NewGuid(), challenge, "012345", digest));
        Assert.IsFalse(codes.Matches(1, owner, Guid.NewGuid(), "012345", digest));
        Assert.IsFalse(codes.Matches(1, owner, challenge, "012346", digest));
        Assert.IsFalse(codes.Matches(2, owner, challenge, "012345", digest));
    }
}
