using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ReFrog.Contracts.Api;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Volunteer;

public sealed record VolunteerActor(Guid AccountId, bool HoldsVolunteerRole);

public sealed record VolunteerAccess(VolunteerActor? Actor, int StatusCode, ApiError? Refusal)
{
    public bool Granted => Refusal is null && Actor is not null;
}

/// <summary>
/// Authenticates and verifies volunteer eligibility for participant-facing volunteer endpoints
/// (VOL.md "Reuse & cross-cutting"; <c>FR-AUTH-enforce-permissions</c>, <c>FR-AUTH-volunteer-lifecycle</c>).
/// </summary>
public sealed class VolunteerAuthorization(
    UserManager<AuthAccount> accounts,
    ILogger<VolunteerAuthorization> logger)
{
    public const string VolunteerRole = "Volunteer";
    public const string VolunteerRestrictedRole = "VolunteerRestricted";

    private static readonly Action<ILogger, Exception?> LogUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7302, "VolunteerAuthorizationUnavailable"),
            "Volunteer authorization state could not be established");

    public async Task<VolunteerAccess> RequireVolunteerUserAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Refuse(401, ApiErrorCategory.Authorization, "AUTHENTICATION_REQUIRED", "Sign in to continue.");
        }

        try
        {
            var account = await accounts.GetUserAsync(principal);
            if (account is null)
            {
                return Refuse(401, ApiErrorCategory.Authorization, "AUTHENTICATION_REQUIRED", "Sign in to continue.");
            }

            // 1a guard: Volunteer privileges revoked or restricted
            if (await accounts.IsInRoleAsync(account, VolunteerRestrictedRole))
            {
                return Refuse(403, ApiErrorCategory.Authorization, "VOLUNTEER_ACCESS_INACTIVE",
                    "Volunteer access is inactive. Administrator review is required.");
            }

            var holdsRole = await accounts.IsInRoleAsync(account, VolunteerRole);
            return new VolunteerAccess(new VolunteerActor(account.Id, holdsRole), 200, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnavailable(logger, exception);
            return Refuse(503, ApiErrorCategory.Unavailable, "ACCESS_UNAVAILABLE",
                "Volunteer access is temporarily unavailable. Try again.");
        }
    }

    private static VolunteerAccess Refuse(int status, ApiErrorCategory category, string code, string message) =>
        new(null, status, new ApiError(category, code, message));
}
