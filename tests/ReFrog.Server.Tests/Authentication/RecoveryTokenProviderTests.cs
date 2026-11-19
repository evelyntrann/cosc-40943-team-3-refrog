using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReFrog.Server.Configuration;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class RecoveryTokenProviderTests
{
    [TestMethod]
    public async Task TokensUseInjectedClockAndExpireAtThirtyMinutes()
    {
        using var services = CreateServices();
        var manager = services.GetRequiredService<UserManager<AuthAccount>>();
        var clock = new ManualClock(new DateTimeOffset(2026, 11, 18, 18, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(clock);
        var user = Account();
        var token = await provider.GenerateAsync("ResetPassword", manager, user);

        Assert.IsTrue(await provider.ValidateAsync("ResetPassword", token, manager, user));
        clock.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromTicks(1));
        Assert.IsTrue(await provider.ValidateAsync("ResetPassword", token, manager, user));
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.IsFalse(await provider.ValidateAsync("ResetPassword", token, manager, user));
    }

    [TestMethod]
    public async Task TokensRejectOtherAccountPurposeStampAndMalformedInput()
    {
        using var services = CreateServices();
        var manager = services.GetRequiredService<UserManager<AuthAccount>>();
        var provider = CreateProvider(TimeProvider.System);
        var user = Account();
        var token = await provider.GenerateAsync("ResetPassword", manager, user);

        Assert.IsFalse(await provider.ValidateAsync("OtherPurpose", token, manager, user));
        Assert.IsFalse(await provider.ValidateAsync("ResetPassword", token, manager, Account()));
        Assert.IsFalse(await provider.ValidateAsync("ResetPassword", "invalid", manager, user));
        Assert.IsFalse(await provider.ValidateAsync("ResetPassword", Convert.ToBase64String(new byte[32]), manager, user));
        user.SecurityStamp = Guid.NewGuid().ToString();
        Assert.IsFalse(await provider.ValidateAsync("ResetPassword", token, manager, user));
    }

    [TestMethod]
    public async Task PayloadRemainsCompatibleWithIdentityDataProtectionTokens()
    {
        using var services = CreateServices();
        var manager = services.GetRequiredService<UserManager<AuthAccount>>();
        var protection = new EphemeralDataProtectionProvider();
        var options = Options.Create(new DataProtectionTokenProviderOptions());
        var logger = NullLogger<DataProtectorTokenProvider<AuthAccount>>.Instance;
        var provider = new RecoveryTokenProvider(protection, options, logger, TimeProvider.System);
        var identity = new DataProtectorTokenProvider<AuthAccount>(protection, options, logger);
        var user = Account();

        Assert.IsTrue(await identity.ValidateAsync("ResetPassword",
            await provider.GenerateAsync("ResetPassword", manager, user), manager, user));
        Assert.IsTrue(await provider.ValidateAsync("ResetPassword",
            await identity.GenerateAsync("ResetPassword", manager, user), manager, user));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReFrogCore(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private static AuthAccount Account() => new()
    {
        Id = Guid.NewGuid(),
        SecurityStamp = Guid.NewGuid().ToString(),
    };

    private static RecoveryTokenProvider CreateProvider(TimeProvider clock) => new(
        new EphemeralDataProtectionProvider(), Options.Create(new DataProtectionTokenProviderOptions()),
        NullLogger<DataProtectorTokenProvider<AuthAccount>>.Instance, clock);
}
