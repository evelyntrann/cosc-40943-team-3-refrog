using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ReFrog.Server.Features.Authentication.Persistence;

/// <summary>
/// SQL Server storage for verification challenges of every purpose, counted by owner
/// (AUTH.md "SQL Server concurrency uses conditional writes and constraints", "Limits by owner").
/// Callers own the transaction; this class never begins one.
/// </summary>
public sealed class EfVerificationChallengeStore(AuthDbContext db, VerificationCode codes, TimeProvider clock)
    : IVerificationChallengeStore
{
    public Task<VerificationChallenge?> FindByRequestIdAsync(ChallengeOwner owner, Guid requestId,
        CancellationToken cancellationToken) =>
        Owned(owner).SingleOrDefaultAsync(challenge => challenge.RequestId == requestId, cancellationToken);

    public async Task<bool> RecordSendOutcomeAsync(Guid challengeId, EmailDeliveryOutcome outcome, CancellationToken cancellationToken)
    {
        var updated = await db.VerificationChallenges.Where(challenge => challenge.Id == challengeId && challenge.SendOutcome == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.SendOutcome, outcome.ToString().ToUpperInvariant()), cancellationToken);
        return updated == 1;
    }

    /// <summary>
    /// Applies the request limits, supersedes the owner's live challenge, and adds a new one to the
    /// context. The caller saves and commits it inside its serializable transaction.
    /// </summary>
    public async Task<IssueResult> IssueAsync(ChallengeOwner owner, string normalizedEmail, Guid requestId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var windowStart = now - VerificationPolicy.RequestWindow;
        var ownerChallenges = await Owned(owner)
            .Where(challenge => challenge.CreatedAt > windowStart)
            .OrderBy(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);
        var destinationChallenges = await db.VerificationChallenges
            .Where(challenge => challenge.NormalizedEmail == normalizedEmail && challenge.CreatedAt > windowStart)
            .OrderBy(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);
        var latest = ownerChallenges.LastOrDefault();
        DateTimeOffset? retryAt = null;
        if (latest is not null && latest.CreatedAt + VerificationPolicy.ResendSpacing > now)
        {
            retryAt = latest.CreatedAt + VerificationPolicy.ResendSpacing;
        }

        if (ownerChallenges.Count >= VerificationPolicy.MaxRequestsPerWindow)
        {
            retryAt = Max(retryAt, ownerChallenges[0].CreatedAt + VerificationPolicy.RequestWindow);
        }

        if (destinationChallenges.Count >= VerificationPolicy.MaxRequestsPerWindow)
        {
            retryAt = Max(retryAt, destinationChallenges[0].CreatedAt + VerificationPolicy.RequestWindow);
        }

        if (retryAt is not null)
        {
            return new(retryAt, null);
        }

        var active = await Owned(owner).SingleOrDefaultAsync(
            challenge => challenge.ConsumedAt == null && challenge.SupersededAt == null, cancellationToken);
        if (active is not null)
        {
            active.SupersededAt = now;
        }

        var challengeId = Guid.NewGuid();
        var code = codes.Generate();
        while (active is not null && codes.Matches(active.KeyVersion, owner, active.Id, code, active.CodeHmac))
        {
            code = codes.Generate();
        }

        var challenge = new VerificationChallenge
        {
            Id = challengeId,
            Purpose = owner.Purpose,
            PendingRegistrationId = owner.IsRegistration ? owner.Id : null,
            AccountId = owner.IsRegistration ? null : owner.Id,
            NormalizedEmail = normalizedEmail,
            CodeHmac = codes.Hash(owner, challengeId, code),
            KeyVersion = codes.KeyVersion,
            RequestId = requestId,
            CreatedAt = now,
            ExpiresAt = now + VerificationPolicy.CodeLifetime,
        };
        db.VerificationChallenges.Add(challenge);
        return new(null, new IssuedCode(challengeId, code, challenge.ExpiresAt, now + VerificationPolicy.ResendSpacing));
    }

    /// <summary>
    /// Checks a code against the owner's live challenge. A match is consumed by a conditional write; the caller
    /// finishes its own work and commits. A wrong code is counted and committed here, as registration always did.
    /// </summary>
    public async Task<CodeCheck> CheckAsync(ChallengeOwner owner, string code, IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await Owned(owner).AsNoTracking()
            .Where(item => item.ConsumedAt == null && item.SupersededAt == null)
            .SingleOrDefaultAsync(cancellationToken);
        if (challenge is null || now >= challenge.ExpiresAt)
        {
            return new(CodeCheckOutcome.Expired, now);
        }

        if (await LimitedUntilAsync(owner, now, VerificationPolicy.MaxRecentFailures, cancellationToken) is { } retryAt)
        {
            return new(CodeCheckOutcome.Limited, now, retryAt);
        }

        if (!codes.HasKey(challenge.KeyVersion))
        {
            return new(CodeCheckOutcome.KeyRetired, now);
        }

        if (!codes.Matches(challenge.KeyVersion, owner, challenge.Id, code, challenge.CodeHmac))
        {
            return await RecordFailureAsync(owner, challenge.Id, now, transaction, cancellationToken);
        }

        // AUTH.md key decision: conditional writes, not lock hints. Exactly one concurrent request consumes it.
        var consumed = await db.VerificationChallenges
            .Where(item => item.Id == challenge.Id && item.ConsumedAt == null && item.SupersededAt == null && item.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ConsumedAt, now), cancellationToken);
        return new(consumed == 0 ? CodeCheckOutcome.LostRace : CodeCheckOutcome.Matched, now);
    }

    private async Task<CodeCheck> RecordFailureAsync(ChallengeOwner owner, Guid challengeId, DateTimeOffset now,
        IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        // Atomic increment: concurrent wrong codes cannot overwrite each other's count, and the
        // fifth wrong code supersedes the challenge in the same statement (FR-AUTH-verification-limits).
        var counted = await db.VerificationChallenges
            .Where(item => item.Id == challengeId && item.ConsumedAt == null && item.SupersededAt == null &&
                item.FailedAttempts < VerificationPolicy.MaxWrongPerCode)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1)
                .SetProperty(item => item.SupersededAt,
                    item => item.FailedAttempts + 1 >= VerificationPolicy.MaxWrongPerCode ? now : item.SupersededAt),
                cancellationToken);
        if (counted == 0)
        {
            return new(CodeCheckOutcome.LostRace, now);
        }

        db.VerificationFailures.Add(new VerificationFailure { Id = Guid.NewGuid(), ChallengeId = challengeId, FailedAt = now });
        await db.SaveChangesAsync(cancellationToken);

        // Re-count with this failure included; the challenge row is locked until commit, so
        // concurrent wrong codes for this owner are counted one at a time.
        if (await LimitedUntilAsync(owner, now, VerificationPolicy.MaxRecentFailures + 1, cancellationToken) is { } retryAt)
        {
            return new(CodeCheckOutcome.Limited, now, retryAt);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(CodeCheckOutcome.Invalid, now);
    }

    private async Task<DateTimeOffset?> LimitedUntilAsync(ChallengeOwner owner, DateTimeOffset now, int threshold,
        CancellationToken cancellationToken)
    {
        var window = now - VerificationPolicy.FailureWindow;
        var recent = OwnedFailures(owner).AsNoTracking().Where(failure => failure.FailedAt > window);
        if (await recent.CountAsync(cancellationToken) < threshold)
        {
            return null;
        }

        var first = await recent.OrderBy(failure => failure.FailedAt).Select(failure => failure.FailedAt).FirstAsync(cancellationToken);
        return first + VerificationPolicy.FailureWindow;
    }

    private IQueryable<VerificationChallenge> Owned(ChallengeOwner owner)
    {
        var id = owner.Id;
        return owner.IsRegistration
            ? db.VerificationChallenges.Where(challenge => challenge.PendingRegistrationId == id)
            : db.VerificationChallenges.Where(challenge => challenge.AccountId == id);
    }

    private IQueryable<VerificationFailure> OwnedFailures(ChallengeOwner owner)
    {
        var id = owner.Id;
        return owner.IsRegistration
            ? db.VerificationFailures.Where(failure => failure.Challenge.PendingRegistrationId == id)
            : db.VerificationFailures.Where(failure => failure.Challenge.AccountId == id);
    }

    private static DateTimeOffset Max(DateTimeOffset? first, DateTimeOffset second) =>
        first is null || second > first ? second : first.Value;
}
