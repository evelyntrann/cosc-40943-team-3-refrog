namespace ReFrog.Contracts.Authentication;

public sealed record StartRegistrationRequest(string Email, string Password, Guid RequestId);

public sealed record ResendRegistrationCodeRequest(Guid RequestId);

public sealed record VerifyRegistrationRequest(string Code);

public sealed record RegistrationChallengeResponse(Guid RegistrationId, DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt);

public sealed record RegisteredAccountResponse(Guid AccountId, string Email, bool TcuEmailVerified);
