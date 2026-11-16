using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Authentication;

public sealed record SessionReply<T>(int StatusCode, ApiResponse<T> Response);

public sealed class SessionService(
    UserManager<AuthAccount> accounts,
    SignInManager<AuthAccount> signIn,
    IAdministratorSignInAudit administratorAudit,
    TimeProvider clock,
    ILogger<SessionService> logger)
{
    private const string AdministratorRole = "Administrator";
    private const int FailedAttemptLimit = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    private static readonly Action<ILogger, Exception?> LogSignInUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7101, "SignInUnavailable"),
            "Authentication state could not be established");
    private static readonly Action<ILogger, Exception?> LogCurrentSessionUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7102, "CurrentSessionUnavailable"),
            "Current authentication state could not be established");
    private static readonly Action<ILogger, Exception?> LogSignOutUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7103, "SignOutUnavailable"),
            "The current session could not be signed out");

    public async Task<SessionReply<SignInResponse>> SignInAsync(
        SignInRequest request, CancellationToken cancellationToken)
    {
        var fields = Validate(request);
        if (fields.Count > 0)
        {
            return Failure<SignInResponse>(422, ApiErrorCategory.Validation, "VALIDATION_FAILED",
                "Enter an email address and password.", fields);
        }

        try
        {
            var account = await accounts.FindByEmailAsync(request.Email.Trim());
            if (account is null)
            {
                return InvalidCredentials();
            }

            var isAdministrator = await accounts.IsInRoleAsync(account, AdministratorRole);
            var now = clock.GetUtcNow();
            var lockoutEnd = await accounts.GetLockoutEndDateAsync(account);
            if (lockoutEnd is not null && lockoutEnd > now)
            {
                if (!await RecordAdministratorAttemptAsync(account.Id, isAdministrator, false, cancellationToken))
                {
                    return Unavailable<SignInResponse>();
                }

                return InvalidCredentials();
            }

            if (!await accounts.CheckPasswordAsync(account, request.Password))
            {
                var failures = await accounts.GetAccessFailedCountAsync(account) + 1;
                IdentityResult failureResult;
                if (failures >= FailedAttemptLimit)
                {
                    failureResult = await accounts.SetLockoutEndDateAsync(account, now.Add(LockoutDuration));
                    if (failureResult.Succeeded)
                    {
                        failureResult = await accounts.ResetAccessFailedCountAsync(account);
                    }
                }
                else
                {
                    failureResult = await accounts.AccessFailedAsync(account);
                }

                if (!failureResult.Succeeded ||
                    !await RecordAdministratorAttemptAsync(account.Id, isAdministrator, false, cancellationToken))
                {
                    return Unavailable<SignInResponse>();
                }

                return InvalidCredentials();
            }

            var resetFailures = await accounts.ResetAccessFailedCountAsync(account);
            var clearLockout = await accounts.SetLockoutEndDateAsync(account, null);
            if (!resetFailures.Succeeded || !clearLockout.Succeeded ||
                !await RecordAdministratorAttemptAsync(account.Id, isAdministrator, true, cancellationToken))
            {
                return Unavailable<SignInResponse>();
            }

            var lifetime = isAdministrator ? TimeSpan.FromHours(4) : TimeSpan.FromDays(30);
            await signIn.SignInAsync(account, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = now.Add(lifetime),
                AllowRefresh = isAdministrator,
            });

            return new(200, ApiResponse.Success(new SignInResponse(account.Id, account.Email!, isAdministrator,
                SafeDestination(request.ReturnPath))));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSignInUnavailable(logger, null);
            return Unavailable<SignInResponse>();
        }
    }

    public async Task<SessionReply<CurrentSessionResponse>> CurrentAsync(
        System.Security.Claims.ClaimsPrincipal principal)
    {
        try
        {
            var account = await accounts.GetUserAsync(principal);
            if (account is null)
            {
                return Failure<CurrentSessionResponse>(401, ApiErrorCategory.Authorization, "AUTHENTICATION_REQUIRED",
                    "Sign in to continue.");
            }

            var isAdministrator = await accounts.IsInRoleAsync(account, AdministratorRole);
            return new(200, ApiResponse.Success(new CurrentSessionResponse(account.Id, account.Email!, isAdministrator)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCurrentSessionUnavailable(logger, null);
            return Unavailable<CurrentSessionResponse>();
        }
    }

    public async Task<SessionReply<SignOutResponse>> SignOutAsync(
        System.Security.Claims.ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Failure<SignOutResponse>(401, ApiErrorCategory.Authorization, "AUTHENTICATION_REQUIRED",
                "Sign in to continue.");
        }

        try
        {
            await signIn.SignOutAsync();
            return new(200, ApiResponse.Success(new SignOutResponse(true)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSignOutUnavailable(logger, null);
            return Failure<SignOutResponse>(503, ApiErrorCategory.Unavailable, "SIGN_OUT_UNAVAILABLE",
                "Sign-out is temporarily unavailable. Try again.");
        }
    }

    private async Task<bool> RecordAdministratorAttemptAsync(
        Guid accountId, bool isAdministrator, bool succeeded, CancellationToken cancellationToken)
    {
        if (!isAdministrator)
        {
            return true;
        }

        await administratorAudit.RecordAsync(accountId, succeeded, cancellationToken);
        return true;
    }

    private static Dictionary<string, string> Validate(SignInRequest request)
    {
        var fields = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            fields["email"] = "Enter your email address.";
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            fields["password"] = "Enter your password.";
        }

        return fields;
    }

    private static string SafeDestination(string? returnPath) =>
        !string.IsNullOrWhiteSpace(returnPath) && returnPath[0] == '/' &&
        !returnPath.StartsWith("//", StringComparison.Ordinal) &&
        !returnPath.StartsWith("/\\", StringComparison.Ordinal)
            ? returnPath
            : "/account";

    private static SessionReply<SignInResponse> InvalidCredentials() =>
        Failure<SignInResponse>(401, ApiErrorCategory.Authorization, "INVALID_CREDENTIALS",
            "The email or password was not accepted.");

    private static SessionReply<T> Unavailable<T>() =>
        Failure<T>(503, ApiErrorCategory.Unavailable, "AUTHENTICATION_UNAVAILABLE",
            "Sign-in is temporarily unavailable. Try again.");

    private static SessionReply<T> Failure<T>(int status, ApiErrorCategory category, string code, string message,
        IReadOnlyDictionary<string, string>? fields = null) =>
        new(status, ApiResponse.Failure<T>(new ApiError(category, code, message, fields)));
}
