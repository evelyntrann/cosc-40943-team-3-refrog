using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Authentication.Persistence;

public sealed class EfRecoveryRepository(
    AuthDbContext db, UserManager<AuthAccount> accounts, TimeProvider clock) : IRecoveryRepository
{
    public async Task RequestAsync(string normalizedEmail,
        Func<string, string, CancellationToken, Task<EmailDeliveryOutcome>> deliver,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Serialize recovery sends before contacting the adapter (FR-AUTH-password-recovery).
        // This no-op update acquires the account write lock without changing its stamp.
        var matched = await db.Users.Where(account => account.NormalizedEmail == normalizedEmail)
            .ExecuteUpdateAsync(update => update.SetProperty(account => account.ConcurrencyStamp,
                account => account.ConcurrencyStamp), cancellationToken);
        if (matched == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var recent = await db.RecoveryRequests.AsNoTracking()
            .Where(request => request.NormalizedEmail == normalizedEmail && request.AcceptedAt > now.AddHours(-1))
            .Select(request => request.AcceptedAt).ToListAsync(cancellationToken);
        if (recent.Count >= 5 || recent.Any(acceptedAt => acceptedAt > now.AddSeconds(-60)))
        {
            return;
        }

        var account = await accounts.FindByEmailAsync(normalizedEmail);
        if (account is null)
        {
            return;
        }

        var token = await accounts.GeneratePasswordResetTokenAsync(account);
        if (await deliver(account.Email!, token, cancellationToken) != EmailDeliveryOutcome.Accepted)
        {
            return;
        }

        db.RecoveryRequests.Add(new RecoveryRequest
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            NormalizedEmail = normalizedEmail,
            AcceptedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PasswordResetOutcome> ResetAsync(
        string normalizedEmail, string token, string newPassword, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = await accounts.FindByEmailAsync(normalizedEmail);
        if (account is null)
        {
            return PasswordResetOutcome.InvalidToken;
        }

        var result = await accounts.ResetPasswordAsync(account, token, newPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal)))
            {
                return PasswordResetOutcome.InvalidPassword;
            }

            return result.Errors.Any(error => error.Code is "InvalidToken" or "ConcurrencyFailure")
                ? PasswordResetOutcome.InvalidToken
                : PasswordResetOutcome.Unavailable;
        }

        await transaction.CommitAsync(cancellationToken);
        return PasswordResetOutcome.Saved;
    }
}
