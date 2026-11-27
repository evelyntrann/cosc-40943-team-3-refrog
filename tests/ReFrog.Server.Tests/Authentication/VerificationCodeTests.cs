using System.Security.Cryptography;
using System.Text;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class VerificationCodeTests
{
    [TestMethod]
    public void GeneratedCodeHasSixDigitsIncludingLeadingZeros()
    {
        var codes = new VerificationCode(new byte[32], 1);

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
        var codes = new VerificationCode(new byte[32], 1);
        var owner = ChallengeOwner.Registration(Guid.NewGuid());
        var challenge = Guid.NewGuid();
        var digest = codes.Hash(owner, challenge, "012345");

        Assert.IsTrue(codes.Matches(1, owner, challenge, "012345", digest));
        Assert.IsFalse(codes.Matches(1, ChallengeOwner.Registration(Guid.NewGuid()), challenge, "012345", digest));
        Assert.IsFalse(codes.Matches(1, owner, Guid.NewGuid(), "012345", digest));
        Assert.IsFalse(codes.Matches(1, owner, challenge, "012346", digest));
        Assert.IsFalse(codes.Matches(2, owner, challenge, "012345", digest));
    }

    // AUTH.md test row "Shared service refactor": a registration code hashed before the extraction still verifies.
    [TestMethod]
    public void RegistrationHashFromBeforeTheSharedServiceStillVerifies()
    {
        var key = Enumerable.Range(1, 32).Select(index => (byte)index).ToArray();
        var registrationId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var stored = HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes($"ACCOUNT_REGISTRATION:{registrationId:D}:{challengeId:D}:004211"));

        var codes = new VerificationCode(key, 1);

        Assert.IsTrue(codes.Matches(1, ChallengeOwner.Registration(registrationId), challengeId, "004211", stored));
    }
}
