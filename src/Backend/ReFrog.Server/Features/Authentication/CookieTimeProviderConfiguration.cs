using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ReFrog.Server.Features.Authentication;

public sealed class CookieTimeProviderConfiguration(TimeProvider clock)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (string.Equals(name, IdentityConstants.ApplicationScheme, StringComparison.Ordinal))
        {
            options.TimeProvider = clock;
        }
    }
}
