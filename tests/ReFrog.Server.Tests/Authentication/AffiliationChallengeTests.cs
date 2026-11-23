using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Authentication;

// AUTH.md "Shared challenge service contract" for TCU_AFFILIATION, and the AUTH test rows
// "TCU verification reader" and "Retired key version". Endpoint behavior is covered in IdentityVerification tests.
[TestClass]
public sealed class AffiliationChallengeTests
{
    private const string Shopper = "shopper@example.org";

    [TestMethod]
    public async Task RequestThenVerifyWritesTheAccountsTcuVerification()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);

        var requested = await RequestAsync(fixture, account, "Kim@TCU.edu");
        Assert.AreEqual(ChallengeRequestOutcome.Sent, requested.Outcome);
        Assert.AreEqual("Kim@TCU.edu", fixture.Email.Sent.Single().Destination);

        var verified = await VerifyAsync(fixture, account, fixture.Email.LastCode);
        Assert.AreEqual(ChallengeVerifyOutcome.Verified, verified.Outcome);
        var row = await fixture.WithAuthAsync(db => db.TcuEmailVerifications.AsNoTracking().SingleAsync());
        Assert.AreEqual(account, row.AccountId);
        Assert.AreEqual("KIM@TCU.EDU", row.NormalizedEmail);
        var challenge = await fixture.WithAuthAsync(db => db.VerificationChallenges.AsNoTracking().SingleAsync());
        Assert.AreEqual(VerificationPurposes.TcuAffiliation, challenge.Purpose);
        Assert.AreEqual(account, challenge.AccountId);
        Assert.IsNull(challenge.PendingRegistrationId);
        Assert.IsNotNull(challenge.ConsumedAt);
    }

    [TestMethod]
    public async Task InvalidEmailWritesAndSendsNothing()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);

        var result = await RequestAsync(fixture, account, "a@frog.tcu.edu");

        Assert.AreEqual(ChallengeRequestOutcome.InvalidEmail, result.Outcome);
        Assert.AreEqual(0, fixture.Email.Count);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
    }

    [TestMethod]
    public async Task ReaderReportsVerificationAndOnlyALiveCodesExpiry()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);

        var empty = await ReadAsync(fixture, account);
        Assert.IsFalse(empty.Verified);
        Assert.IsNull(empty.VerifiedAt);
        Assert.IsNull(empty.PendingCodeExpiresAt);

        await RequestAsync(fixture, account, "kim@tcu.edu");
        var pending = await ReadAsync(fixture, account);
        Assert.AreEqual(AffiliationFixture.Now + VerificationPolicy.CodeLifetime, pending.PendingCodeExpiresAt);

        fixture.Clock.Advance(VerificationPolicy.CodeLifetime);
        Assert.IsNull((await ReadAsync(fixture, account)).PendingCodeExpiresAt, "expired");

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        await RequestAsync(fixture, account, "kim@tcu.edu");
        await fixture.WithAuthAsync(db => db.VerificationChallenges
            .Where(item => item.ConsumedAt == null && item.SupersededAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.FailedAttempts, VerificationPolicy.MaxWrongPerCode)));
        Assert.IsNull((await ReadAsync(fixture, account)).PendingCodeExpiresAt, "five failures");

        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        await RequestAsync(fixture, account, "kim@tcu.edu");
        fixture.Clock.Advance(VerificationPolicy.ResendSpacing);
        await RequestAsync(fixture, account, "kim@tcu.edu");
        var superseded = await fixture.WithAuthAsync(db => db.VerificationChallenges.AsNoTracking()
            .CountAsync(item => item.SupersededAt != null));
        Assert.IsGreaterThanOrEqualTo(1, superseded);
        Assert.AreEqual(fixture.Clock.GetUtcNow() + VerificationPolicy.CodeLifetime,
            (await ReadAsync(fixture, account)).PendingCodeExpiresAt, "only the latest code is live");

        await VerifyAsync(fixture, account, fixture.Email.LastCode);
        var verified = await ReadAsync(fixture, account);
        Assert.IsTrue(verified.Verified);
        Assert.AreEqual(fixture.Clock.GetUtcNow(), verified.VerifiedAt);
        Assert.IsNull(verified.PendingCodeExpiresAt, "consumed");
    }

    [TestMethod]
    public async Task ReaderThrowsUnavailableWhenTheDatabaseFails()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=Unreachable;Connect Timeout=1;TrustServerCertificate=True").Options;
        await using var db = new AuthDbContext(options);
        var reader = new EfTcuVerificationReader(db, TimeProvider.System);

        await Assert.ThrowsExactlyAsync<AccessStateUnavailableException>(
            () => reader.ForAccountAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task RetiredKeyVersionIsNotRecordedAndCountsNoFailure()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);
        await RequestAsync(fixture, account, "kim@tcu.edu");
        await fixture.WithAuthAsync(db => db.VerificationChallenges
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.KeyVersion, 9)));

        var result = await VerifyAsync(fixture, account, fixture.Email.LastCode);

        Assert.AreEqual(ChallengeVerifyOutcome.NotRecorded, result.Outcome);
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationFailures.CountAsync()));
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.VerificationChallenges.SumAsync(item => item.FailedAttempts)));
        Assert.AreEqual(0, await fixture.WithAuthAsync(db => db.TcuEmailVerifications.CountAsync()));
    }

    [TestMethod]
    public async Task CodeUnderThePreviousKeyVersionStillVerifiesAfterRotation()
    {
        var oldKey = Enumerable.Range(1, 32).Select(index => (byte)index).ToArray();
        var newKey = Enumerable.Range(101, 32).Select(index => (byte)index).ToArray();
        var rotated = new VerificationCode(new Dictionary<int, byte[]> { [1] = oldKey, [2] = newKey }, 2);
        await using var fixture = await AffiliationFixture.CreateAsync(services =>
        {
            services.RemoveAll<VerificationCode>();
            services.AddSingleton(rotated);
        });
        var account = await fixture.CreateAccountAsync(Shopper);
        var challengeId = Guid.NewGuid();
        await fixture.WithAuthAsync(async db =>
        {
            db.VerificationChallenges.Add(new VerificationChallenge
            {
                Id = challengeId,
                Purpose = VerificationPurposes.TcuAffiliation,
                AccountId = account,
                NormalizedEmail = "KIM@TCU.EDU",
                CodeHmac = new VerificationCode(oldKey, 1).Hash(ChallengeOwner.Affiliation(account), challengeId, "123456"),
                KeyVersion = 1,
                RequestId = Guid.NewGuid(),
                SendOutcome = "ACCEPTED",
                CreatedAt = AffiliationFixture.Now,
                ExpiresAt = AffiliationFixture.Now + VerificationPolicy.CodeLifetime,
            });
            return await db.SaveChangesAsync();
        });

        var result = await VerifyAsync(fixture, account, "123456");

        Assert.AreEqual(ChallengeVerifyOutcome.Verified, result.Outcome);
    }

    [TestMethod]
    public async Task AlreadyVerifiedAccountGetsNoNewCode()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync(Shopper);
        await RequestAsync(fixture, account, "kim@tcu.edu");
        await VerifyAsync(fixture, account, fixture.Email.LastCode);
        fixture.Clock.Advance(TimeSpan.FromHours(2));

        var result = await RequestAsync(fixture, account, "other@tcu.edu");

        Assert.AreEqual(ChallengeRequestOutcome.AlreadyVerified, result.Outcome);
        Assert.AreEqual(1, fixture.Email.Count);
        Assert.AreEqual(1, await fixture.WithAuthAsync(db => db.VerificationChallenges.CountAsync()));
    }

    private static Task<ChallengeRequestResult> RequestAsync(AffiliationFixture fixture, Guid account, string email) =>
        fixture.WithServicesAsync(services => services.GetRequiredService<VerificationChallengeService>()
            .RequestTcuAffiliationAsync(account, email, Guid.NewGuid(), CancellationToken.None));

    private static Task<ChallengeVerifyResult> VerifyAsync(AffiliationFixture fixture, Guid account, string code) =>
        fixture.WithServicesAsync(services => services.GetRequiredService<VerificationChallengeService>()
            .VerifyTcuAffiliationAsync(account, code, CancellationToken.None));

    private static Task<TcuVerificationState> ReadAsync(AffiliationFixture fixture, Guid account) =>
        fixture.WithServicesAsync(services => services.GetRequiredService<ITcuVerificationReader>()
            .ForAccountAsync(account, CancellationToken.None));
}
