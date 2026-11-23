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
    EfVerificationChallengeStore challenges,
    TimeProvider clock,
    IConfiguration configuration) : IAuthRepository
{

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

            var original = await challenges.FindByRequestIdAsync(
                ChallengeOwner.Registration(pending.Id), requestId, cancellationToken);
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
            Email = email,
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

        var existing = await challenges.FindByRequestIdAsync(
            ChallengeOwner.Registration(registrationId), requestId, cancellationToken);
        if (existing is not null)
        {
            return Replay(pending, existing);
        }

        var issued = await IssueAsync(pending, pending.Email, requestId, cancellationToken);
        if (issued.Outcome == RegistrationOutcome.Issued)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return issued;
    }

    // AUTH.md key decision: verification uses conditional writes, not lock hints. Each write below
    // only succeeds while its row is still in the expected state, so exactly one concurrent request
    // consumes a challenge or completes a registration; the others see 0 rows and stop.
    public async Task<RegistrationResult> VerifyAsync(Guid registrationId, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var pending = await db.PendingRegistrations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == registrationId, cancellationToken);
        if (pending is null)
        {
            return new(RegistrationOutcome.NotFound);
        }

        if (pending.State != "PENDING")
        {
            return new(RegistrationOutcome.Completed);
        }

        var check = await challenges.CheckAsync(ChallengeOwner.Registration(registrationId), code, transaction, cancellationToken);
        var now = check.At;
        switch (check.Outcome)
        {
            case CodeCheckOutcome.Expired:
                return new(RegistrationOutcome.ExpiredCode);
            case CodeCheckOutcome.Limited:
                return new(RegistrationOutcome.Limited, RetryAt: check.RetryAt);
            case CodeCheckOutcome.KeyRetired:
                return new(RegistrationOutcome.Unavailable);
            case CodeCheckOutcome.Invalid:
                return new(RegistrationOutcome.InvalidCode);
            case CodeCheckOutcome.LostRace:
                return await AfterLostRaceAsync(registrationId, cancellationToken);
        }

        if (await db.Users.AnyAsync(user => user.NormalizedEmail == pending.NormalizedEmail, cancellationToken))
        {
            return new(RegistrationOutcome.Completed);
        }

        var account = new AuthAccount
        {
            Id = Guid.NewGuid(),
            UserName = pending.Email,
            Email = pending.Email,
            NormalizedEmail = pending.NormalizedEmail,
            EmailConfirmed = true,
            PasswordHash = pending.PasswordHash,
            LockoutEnabled = true,
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

        var tcu = TcuEmailAddress.HasExactTcuDomain(pending.NormalizedEmail);
        if (tcu)
        {
            db.TcuEmailVerifications.Add(new TcuEmailVerification
            {
                AccountId = account.Id,
                NormalizedEmail = pending.NormalizedEmail,
                VerifiedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        var completed = await db.PendingRegistrations
            .Where(item => item.Id == registrationId && item.State == "PENDING")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, "COMPLETED")
                .SetProperty(item => item.AccountId, account.Id), cancellationToken);
        if (completed == 0)
        {
            return new(RegistrationOutcome.Completed);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(RegistrationOutcome.Saved, Account: new(account.Id, account.Email, tcu), IsAdministrator: isAdministrator);
    }

    // Another request changed the challenge first; report what it left behind.
    private async Task<RegistrationResult> AfterLostRaceAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var state = await db.PendingRegistrations.AsNoTracking()
            .Where(item => item.Id == registrationId).Select(item => item.State).SingleAsync(cancellationToken);
        return new(state == "PENDING" ? RegistrationOutcome.ExpiredCode : RegistrationOutcome.Completed);
    }

    private async Task<RegistrationResult> IssueAsync(
        PendingRegistration pending, string email, Guid requestId, CancellationToken cancellationToken)
    {
        var result = await challenges.IssueAsync(
            ChallengeOwner.Registration(pending.Id), pending.NormalizedEmail, requestId, cancellationToken);
        if (result.Issued is not { } issued)
        {
            return new(RegistrationOutcome.Limited, RetryAt: result.RetryAt);
        }

        return new(RegistrationOutcome.Issued,
            new(pending.Id, issued.ExpiresAt, issued.ResendAvailableAt),
            Email: email, Issued: issued);
    }

    private static RegistrationResult Replay(PendingRegistration pending, VerificationChallenge challenge) =>
        new(RegistrationOutcome.Replayed,
            new(pending.Id, challenge.ExpiresAt, challenge.CreatedAt + VerificationPolicy.ResendSpacing),
            SendOutcome: challenge.SendOutcome);

}
