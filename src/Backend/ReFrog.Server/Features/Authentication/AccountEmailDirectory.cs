using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Authentication;

/// <summary>
/// Account emails for other areas, so they never read AUTH's tables (AUTH.md Components). Only ADM's
/// administrator-only preview of a shift change calls it (<c>UC-ADM-manage-shifts</c> 6a1). It is not an
/// authorization check.
/// </summary>
public interface IAccountEmailDirectory
{
    /// <summary>Emails by account id. An unknown id is left out of the answer.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
}

public sealed class EfAccountEmailDirectory(AuthDbContext db) : IAccountEmailDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);
        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = accountIds.Distinct().ToList();
        var accounts = await db.Users.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.Email != null)
            .Select(x => new { x.Id, x.Email })
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(x => x.Id, x => x.Email!);
    }
}
