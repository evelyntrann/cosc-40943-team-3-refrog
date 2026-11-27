using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Authentication.Persistence;

/// <summary>Reads persisted TCU verification for <see cref="ITcuVerificationReader"/>; it never exposes the address.</summary>
public sealed class EfTcuVerificationReader(AuthDbContext db, TimeProvider clock) : ITcuVerificationReader
{
    public async Task<TcuVerificationState> ForAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            var verifiedAt = await db.TcuEmailVerifications.AsNoTracking()
                .Where(item => item.AccountId == accountId)
                .Select(item => (DateTimeOffset?)item.VerifiedAt)
                .SingleOrDefaultAsync(cancellationToken);
            var now = clock.GetUtcNow();
            var pendingExpiresAt = await db.VerificationChallenges.AsNoTracking()
                .Where(item => item.Purpose == VerificationPurposes.TcuAffiliation && item.AccountId == accountId &&
                    item.ConsumedAt == null && item.SupersededAt == null &&
                    item.FailedAttempts < VerificationPolicy.MaxWrongPerCode && item.ExpiresAt > now)
                .Select(item => (DateTimeOffset?)item.ExpiresAt)
                .SingleOrDefaultAsync(cancellationToken);
            return new(verifiedAt is not null, verifiedAt, pendingExpiresAt);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            throw new AccessStateUnavailableException(failure);
        }
    }
}
