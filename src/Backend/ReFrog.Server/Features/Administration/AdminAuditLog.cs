using ReFrog.Server.Features.Administration.Persistence;

namespace ReFrog.Server.Features.Administration;

/// <summary>
/// Records an administrator change to shifts and location defaults (architecture §8.2.9, ADM.md key decision 7).
/// The entry is staged on ADM's store and saved by the same <c>SaveChanges</c>, inside the same transaction, as
/// the change it describes: a rolled-back change leaves no entry and a committed one always has it.
/// Administrator sign-in attempts are AUTH's <c>IAdministratorSignInAudit</c>, not this.
/// </summary>
public interface IAdminAuditLog
{
    void Record(Guid actorAccountId, string action, string targetType, string targetId);
}

public sealed class EfAdminAuditLog(AdmDbContext db, TimeProvider clock) : IAdminAuditLog
{
    public void Record(Guid actorAccountId, string action, string targetType, string targetId) =>
        db.AuditEntries.Add(new AdminAuditEntry
        {
            Id = Guid.NewGuid(),
            ActorAccountId = actorAccountId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OccurredAt = clock.GetUtcNow(),
        });
}
