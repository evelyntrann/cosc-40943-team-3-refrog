namespace ReFrog.Contracts.Authentication;

public sealed record RequestRecoveryRequest(string Email);

public sealed record RecoveryAcknowledgement(string Message);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record PasswordResetResponse(bool PasswordChanged);
