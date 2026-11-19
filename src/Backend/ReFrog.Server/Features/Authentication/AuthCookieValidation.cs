using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Authentication;

public sealed class AuthenticationStateUnavailableException : Exception;

public static class AuthCookieValidation
{
    private static readonly Action<ILogger, Exception?> StateUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7204, "CookieValidationUnavailable"),
            "Current cookie authentication state could not be established");

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var signIn = context.HttpContext.RequestServices.GetRequiredService<SignInManager<AuthAccount>>();
        try
        {
            var account = await signIn.ValidateSecurityStampAsync(context.Principal);
            if (account is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            context.ReplacePrincipal(await signIn.CreateUserPrincipalAsync(account));
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AuthCookieValidation));
            StateUnavailable(logger, null);
            throw new AuthenticationStateUnavailableException();
        }
    }
}
