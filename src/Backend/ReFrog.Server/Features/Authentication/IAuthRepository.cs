using ReFrog.Contracts.Authentication;

namespace ReFrog.Server.Features.Authentication;

public enum RegistrationOutcome
{
    Issued,
    Replayed,
    DuplicateEmail,
    RequestIdReused,
    NotFound,
    Completed,
    Limited,
    InvalidCode,
    ExpiredCode,
    Saved,
    Unavailable,
}

public sealed record RegistrationResult(
    RegistrationOutcome Outcome,
    RegistrationChallengeResponse? Challenge = null,
    RegisteredAccountResponse? Account = null,
    string? Email = null,
    string? Code = null,
    Guid? ChallengeId = null,
    DateTimeOffset? RetryAt = null,
    bool IsAdministrator = false,
    string? SendOutcome = null);

public interface IAuthRepository
{
    Task<RegistrationResult> StartAsync(string email, string normalizedEmail, string password, Guid requestId, CancellationToken cancellationToken);
    Task<RegistrationResult> ResendAsync(Guid registrationId, Guid requestId, CancellationToken cancellationToken);
    Task<bool> RecordSendOutcomeAsync(Guid challengeId, EmailDeliveryOutcome outcome, CancellationToken cancellationToken);
    Task<RegistrationResult> VerifyAsync(Guid registrationId, string code, CancellationToken cancellationToken);
}
