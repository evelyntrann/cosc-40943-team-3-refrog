using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Server.Features.Authentication.Persistence;

public sealed class EfAuthRepository(
    AuthDbContext db,
    UserManager<AuthAccount> accounts,
    RoleManager<IdentityRole<Guid>> roles,
    IPasswordHasher<AuthAccount> passwords,
    RegistrationCode codes,
    TimeProvider clock,
    IConfiguration configuration) : IAuthRepository
{
    private const string RegistrationPurpose = "ACCOUNT_REGISTRATION";

    public async Task<RegistrationResult> StartAsync(
        string email, string normalizedEmail, string password, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        if (await db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return new(RegistrationOutcome.DuplicateEmail);
        }

        var pending = await db.PendingRegistrations.SingleOrDefaultAsync(item => item.StartRequestId == requestId, cancellationToken);
        if (pending is not null)
        {
            if (pending.NormalizedEmail != normalizedEmail ||
                passwords.VerifyHashedPassword(new AuthAccount(), pending.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                return new(RegistrationOutcome.RequestIdReused);
            }

            var original = await db.VerificationChallenges.SingleOrDefaultAsync(
                challenge => challenge.PendingRegistrationId == pending.Id && challenge.RequestId == requestId,
                cancellationToken);
            if (original is null)
            {
                var delayed = await IssueAsync(pending, email, requestId, cancellationToken);
                if (delayed.Outcome == RegistrationOutcome.Issued)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                return delayed;
            }

            return Replay(pending, original);
        }

        pending = new PendingRegistration
        {
            Id = Guid.NewGuid(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwords.HashPassword(new AuthAccount(), password),
            StartRequestId = requestId,
            State = "PENDING",
            CreatedAt = clock.GetUtcNow(),
        };
        db.PendingRegistrations.Add(pending);

        var issued = await IssueAsync(pending, email, requestId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return issued;
    }

    public async Task<RegistrationResult> ResendAsync(Guid registrationId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var pending = await db.PendingRegistrations.SingleOrDefaultAsync(item => item.Id == registrationId, cancellationToken);
        if (pending is null)
        {
            return new(RegistrationOutcome.NotFound);
        }

        if (pending.State != "PENDING")
        {
            return new(RegistrationOutcome.Completed);
        }

        var existing = await db.VerificationChallenges.SingleOrDefaultAsync(
            challenge => challenge.PendingRegistrationId == registrationId && challenge.RequestId == requestId,
            cancellationToken);
        if (existing is not null)
        {
            return Replay(pending, existing);
        }

        var issued = await IssueAsync(pending, pending.NormalizedEmail, requestId, cancellationToken);
        if (issued.Outcome == RegistrationOutcome.Issued)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return issued;
    }

    public async Task<bool> RecordSendOutcomeAsync(Guid challengeId, EmailDeliveryOutcome outcome, CancellationToken cancellationToken)
    {
        var updated = await db.VerificationChallenges.Where(challenge => challenge.Id == challengeId && challenge.SendOutcome == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.SendOutcome, outcome.ToString().ToUpperInvariant()), cancellationToken);
        return updated == 1;
    }

    public async Task<RegistrationResult> VerifyAsync(Guid registrationId, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var pending = await db.PendingRegistrations
            .FromSqlInterpolated($"SELECT * FROM [PendingRegistrations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {registrationId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (pending is null)
        {
            return new(RegistrationOutcome.NotFound);
        }

        if (pending.State != "PENDING")
        {
            return new(RegistrationOutcome.Completed);
        }

        var now = clock.GetUtcNow();
        var challenge = await db.VerificationChallenges
            .Where(item => item.PendingRegistrationId == registrationId && item.ConsumedAt == null && item.SupersededAt == null)
            .SingleOrDefaultAsync(cancellationToken);
        if (challenge is null || now >= challenge.ExpiresAt)
        {
            return new(RegistrationOutcome.ExpiredCode);
        }

        var recentFailures = await db.VerificationFailures.CountAsync(
            failure => failure.Challenge.PendingRegistrationId == registrationId && failure.FailedAt > now.AddMinutes(-15),
            cancellationToken);
        if (recentFailures >= 10)
        {
            var first = await db.VerificationFailures
                .Where(failure => failure.Challenge.PendingRegistrationId == registrationId && failure.FailedAt > now.AddMinutes(-15))
                .OrderBy(failure => failure.FailedAt)
                .Select(failure => failure.FailedAt)
                .FirstAsync(cancellationToken);
            return new(RegistrationOutcome.Limited, RetryAt: first.AddMinutes(15));
        }

        if (!codes.HasKey(challenge.KeyVersion))
        {
            return new(RegistrationOutcome.Unavailable);
        }

        if (!codes.Matches(challenge.KeyVersion, registrationId, challenge.Id, code, challenge.CodeHmac))
        {
            challenge.FailedAttempts++;
            if (challenge.FailedAttempts >= 5)
            {
                challenge.SupersededAt = now;
            }

            db.VerificationFailures.Add(new VerificationFailure
            {
                Id = Guid.NewGuid(),
                ChallengeId = challenge.Id,
                FailedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(RegistrationOutcome.InvalidCode);
        }

        if (await db.Users.AnyAsync(user => user.NormalizedEmail == pending.NormalizedEmail, cancellationToken))
        {
            return new(RegistrationOutcome.Completed);
        }

        var account = new AuthAccount
        {
            Id = Guid.NewGuid(),
            UserName = pending.NormalizedEmail,
            Email = pending.NormalizedEmail,
            NormalizedEmail = pending.NormalizedEmail,
            EmailConfirmed = true,
            PasswordHash = pending.PasswordHash,
        };
        var created = await accounts.CreateAsync(account);
        if (!created.Succeeded)
        {
            return new(RegistrationOutcome.Unavailable);
        }

        var isAdministrator = configuration.GetSection("Authentication:AdministratorEmails")
            .Get<string[]>()?.Any(item => !string.IsNullOrWhiteSpace(item) &&
                string.Equals(item.Trim(), pending.NormalizedEmail, StringComparison.OrdinalIgnoreCase)) == true;
        if (isAdministrator)
        {
            if (!await roles.RoleExistsAsync("Administrator"))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole<Guid>("Administrator"));
                if (!roleResult.Succeeded)
                {
                    return new(RegistrationOutcome.Unavailable);
                }
            }

            var roleAssignment = await accounts.AddToRoleAsync(account, "Administrator");
            if (!roleAssignment.Succeeded)
            {
                return new(RegistrationOutcome.Unavailable);
            }
        }

        var tcu = pending.NormalizedEmail.EndsWith("@TCU.EDU", StringComparison.Ordinal);
        if (tcu)
        {
            db.TcuEmailVerifications.Add(new TcuEmailVerification
            {
                AccountId = account.Id,
                NormalizedEmail = pending.NormalizedEmail,
                VerifiedAt = now,
            });
        }

        challenge.ConsumedAt = now;
        pending.AccountId = account.Id;
        pending.State = "COMPLETED";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(RegistrationOutcome.Saved, Account: new(account.Id, account.Email!, tcu), IsAdministrator: isAdministrator);
    }

    private async Task<RegistrationResult> IssueAsync(
        PendingRegistration pending, string email, Guid requestId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var subjectChallenges = await db.VerificationChallenges
            .Where(challenge => challenge.PendingRegistrationId == pending.Id && challenge.CreatedAt > now.AddHours(-1))
            .OrderBy(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);
        var destinationChallenges = await db.VerificationChallenges
            .Where(challenge => challenge.NormalizedEmail == pending.NormalizedEmail && challenge.CreatedAt > now.AddHours(-1))
            .OrderBy(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);
        var latest = subjectChallenges.LastOrDefault();
        DateTimeOffset? retryAt = null;
        if (latest is not null && latest.CreatedAt.AddSeconds(60) > now)
        {
            retryAt = latest.CreatedAt.AddSeconds(60);
        }

        if (subjectChallenges.Count >= 5)
        {
            retryAt = Max(retryAt, subjectChallenges[0].CreatedAt.AddHours(1));
        }

        if (destinationChallenges.Count >= 5)
        {
            retryAt = Max(retryAt, destinationChallenges[0].CreatedAt.AddHours(1));
        }

        if (retryAt is not null)
        {
            return new(RegistrationOutcome.Limited, RetryAt: retryAt);
        }

        var active = await db.VerificationChallenges.SingleOrDefaultAsync(
            challenge => challenge.PendingRegistrationId == pending.Id &&
                challenge.ConsumedAt == null && challenge.SupersededAt == null,
            cancellationToken);
        if (active is not null)
        {
            active.SupersededAt = now;
        }

        var challengeId = Guid.NewGuid();
        var code = codes.Generate();
        while (active is not null && codes.Matches(active.KeyVersion, pending.Id, active.Id, code, active.CodeHmac))
        {
            code = codes.Generate();
        }
        var challenge = new VerificationChallenge
        {
            Id = challengeId,
            Purpose = RegistrationPurpose,
            PendingRegistrationId = pending.Id,
            NormalizedEmail = pending.NormalizedEmail,
            CodeHmac = codes.Hash(pending.Id, challengeId, code),
            KeyVersion = codes.KeyVersion,
            RequestId = requestId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
        };
        db.VerificationChallenges.Add(challenge);
        return new(RegistrationOutcome.Issued,
            new(pending.Id, challenge.ExpiresAt, now.AddSeconds(60)),
            Email: email, Code: code, ChallengeId: challengeId);
    }

    private static RegistrationResult Replay(PendingRegistration pending, VerificationChallenge challenge) =>
        new(RegistrationOutcome.Replayed,
            new(pending.Id, challenge.ExpiresAt, challenge.CreatedAt.AddSeconds(60)),
            ChallengeId: challenge.Id,
            SendOutcome: challenge.SendOutcome);

    private static DateTimeOffset Max(DateTimeOffset? first, DateTimeOffset second) =>
        first is null || second > first ? second : first.Value;
}
