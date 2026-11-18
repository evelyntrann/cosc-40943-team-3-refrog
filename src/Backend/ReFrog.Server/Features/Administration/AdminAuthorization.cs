using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ReFrog.Contracts.Api;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Administration;

/// <summary>Either the acting administrator's account id, or the refusal to return (status and error).</summary>
public sealed record AdminAccess(Guid ActorId, int StatusCode, ApiError? Refusal)
{
    public bool Granted => Refusal is null;
}

/// <summary>
/// The one administrator check every ADM endpoint makes (ADM.md key decision 7; <c>SEC-role-based-access</c>,
/// <c>FR-AUTH-enforce-permissions</c>). The account comes from the validated session cookie and the role from a
/// live lookup, never the cookie's role claim alone (AUTH.md "Current user trust"). If AUTH later provides a
/// shared current-user service, only this class changes.
/// </summary>
public sealed class AdminAuthorization(UserManager<AuthAccount> accounts, ILogger<AdminAuthorization> logger)
{
    private static readonly Action<ILogger, Exception?> LogUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7201, "AdministratorCheckUnavailable"),
            "Administrator authorization state could not be established");

    public async Task<AdminAccess> RequireAdministratorAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Refuse(401, "AUTHENTICATION_REQUIRED", "Sign in to continue.");
        }

        try
        {
            var account = await accounts.GetUserAsync(principal);
            if (account is null)
            {
                return Refuse(401, "AUTHENTICATION_REQUIRED", "Sign in to continue.");
            }

            if (!await accounts.IsInRoleAsync(account, SessionService.AdministratorRole))
            {
                return Refuse(403, "ADMINISTRATOR_REQUIRED", "Only ReFrog administrators can do this.");
            }

            return new AdminAccess(account.Id, 200, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // §8.2.1: when authorization state cannot be established, deny as unavailable; never guess.
            LogUnavailable(logger, null);
            return new AdminAccess(Guid.Empty, 503, new ApiError(ApiErrorCategory.Unavailable,
                "AUTHENTICATION_UNAVAILABLE", "Sign-in is temporarily unavailable. Try again."));
        }
    }

    private static AdminAccess Refuse(int status, string code, string message) =>
        new(Guid.Empty, status, new ApiError(ApiErrorCategory.Authorization, code, message));
}
