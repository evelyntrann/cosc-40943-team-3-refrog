namespace ReFrog.Server.Features.Authentication.Persistence;

public sealed class EfAdministratorSignInAudit(AuthDbContext db, TimeProvider clock) : IAdministratorSignInAudit
{
    public async Task RecordAsync(Guid accountId, bool succeeded, CancellationToken cancellationToken)
    {
        db.AdministratorSignInAudits.Add(new AdministratorSignInAudit
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            OccurredAt = clock.GetUtcNow(),
            Succeeded = succeeded,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
