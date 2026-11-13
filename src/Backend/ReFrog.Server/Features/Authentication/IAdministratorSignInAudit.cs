namespace ReFrog.Server.Features.Authentication;

public interface IAdministratorSignInAudit
{
    Task RecordAsync(Guid accountId, bool succeeded, CancellationToken cancellationToken);
}
