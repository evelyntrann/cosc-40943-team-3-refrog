using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ReFrog.Server.Configuration;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Configuration;

[TestClass]
public sealed class ServiceCollectionExtensionsTests
{
    [TestMethod]
    public void CoreServicesProvideTheSystemClock()
    {
        var services = new ServiceCollection();
        services.AddReFrogCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.AreSame(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [TestMethod]
    public void MicrosoftGraphProviderUsesTheSharedEmailAdapter()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "MicrosoftGraph",
            ["Email:MicrosoftGraph:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Email:MicrosoftGraph:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Email:MicrosoftGraph:ClientSecret"] = "test-credential",
            ["Email:MicrosoftGraph:SenderAddress"] = "refrog@example.org",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddReFrogCore(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<MicrosoftGraphEmailSender>(provider.GetRequiredService<IEmailSender>());
    }
}
