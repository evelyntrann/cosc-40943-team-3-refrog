using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Configuration;

namespace ReFrog.Server.Tests.Configuration;

[TestClass]
public sealed class ServiceCollectionExtensionsTests
{
    [TestMethod]
    public void CoreServicesProvideTheSystemClock()
    {
        var services = new ServiceCollection();
        services.AddReFrogCore();

        using var provider = services.BuildServiceProvider();

        Assert.AreSame(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }
}
