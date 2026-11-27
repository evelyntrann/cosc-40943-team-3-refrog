namespace ReFrog.Contracts.IdentityVerification;

/// <summary>Wire values for <see cref="ShoppingEligibilityResponse.Basis"/> (IDV.md API contract).</summary>
public static class EligibilityBasis
{
    public const string TcuEmail = "TCU_EMAIL";
    public const string VolunteerRole = "VOLUNTEER_ROLE";
}

/// <summary>Wire values for <see cref="ShoppingEligibilityResponse.VolunteerAccess"/> (IDV.md API contract).</summary>
public static class VolunteerAccessValues
{
    public const string RoleActive = "ROLE_ACTIVE";
    public const string None = "NONE";
    public const string Restricted = "RESTRICTED";
}

/// <summary>
/// <c>GET /api/v1/me/shopping-eligibility</c>. Never carries a code or the verified email address.
/// </summary>
public sealed record ShoppingEligibilityResponse(
    bool Eligible,
    string? Basis,
    bool TcuEmailVerified,
    string VolunteerAccess,
    DateTimeOffset? PendingCodeExpiresAt);

/// <summary><c>POST /api/v1/me/tcu-email/codes</c>: a new request id for each explicit send, reused only to retry it.</summary>
public sealed record RequestTcuCodeRequest(string? Email, Guid RequestId);

public sealed record TcuCodeResponse(DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt);

/// <summary><c>POST /api/v1/me/tcu-email/verification</c>. The code is a string so leading zeros are kept.</summary>
public sealed record VerifyTcuCodeRequest(string? Code);

/// <summary>The verification success: the same status fields as the GET, plus when it was verified.</summary>
public sealed record TcuVerificationResponse(
    bool Eligible,
    string? Basis,
    bool TcuEmailVerified,
    string VolunteerAccess,
    DateTimeOffset? PendingCodeExpiresAt,
    DateTimeOffset VerifiedAt);
