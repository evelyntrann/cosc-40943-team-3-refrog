namespace ReFrog.Server.Features.Authentication;

public enum PasswordResetOutcome
{
    Saved,
    InvalidToken,
    InvalidPassword,
    Unavailable,
}

public interface IRecoveryRepository
{
    Task RequestAsync(string normalizedEmail,
        Func<string, string, CancellationToken, Task<EmailDeliveryOutcome>> deliver,
        CancellationToken cancellationToken);

    Task<PasswordResetOutcome> ResetAsync(
        string normalizedEmail, string token, string newPassword, CancellationToken cancellationToken);
}
