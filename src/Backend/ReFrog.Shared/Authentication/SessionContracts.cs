namespace ReFrog.Contracts.Authentication;

public sealed record SignInRequest(string Email, string Password, string? ReturnPath = null);

public sealed record SignInResponse(Guid AccountId, string Email, bool IsAdministrator, string Destination);

public sealed record CurrentSessionResponse(Guid AccountId, string Email, bool IsAdministrator);

public sealed record SignOutResponse(bool SignedOut);

public sealed record AntiforgeryTokenResponse(string Token);
