using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.Info;
using ReFrog.Server.Features.Info.Persistence;

namespace ReFrog.Server.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReFrogCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("ReFrog")));
        services.AddIdentityCore<AuthAccount>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            // The user name is the verified email, which registration already validates
            // (FR-AUTH-account-signup: any domain), so Identity's narrower character list must not apply.
            options.User.AllowedUserNameCharacters = string.Empty;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
            });
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.AddAuthorization();
        services.AddAntiforgery();
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<RegistrationService>();
        services.AddDbContext<InfoDbContext>(options =>
            InfoDbContext.Configure(options, configuration.GetConnectionString("ReFrog")));
        services.AddScoped<IEventInfoStore, EfEventInfoStore>();
        services.AddScoped<InfoService>();
        if (string.Equals(configuration["Email:Provider"], "MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IEmailSender, MicrosoftGraphEmailSender>();
        }
        else if (string.Equals(configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, UnavailableEmailSender>();
        }
        services.AddSingleton(provider =>
        {
            var activeVersion = configuration.GetValue<int>("Authentication:ActiveVerificationKeyVersion");
            var keys = configuration.GetSection("Authentication:VerificationKeys").GetChildren()
                .ToDictionary(section => int.Parse(section.Key, System.Globalization.CultureInfo.InvariantCulture),
                    section => Convert.FromBase64String(section.Value ?? string.Empty));
            if (!keys.TryGetValue(activeVersion, out var activeKey) || activeKey.Length < 32 ||
                keys.Values.Any(key => key.Length < 32))
            {
                throw new InvalidOperationException("Verification HMAC keys are not configured.");
            }

            return new RegistrationCode(keys, activeVersion);
        });
        return services;
    }
}
