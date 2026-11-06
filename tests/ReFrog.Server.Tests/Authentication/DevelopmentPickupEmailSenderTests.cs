using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class DevelopmentPickupEmailSenderTests
{
    [TestMethod]
    public async Task WritesOneMessageFileAndReportsAccepted()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"refrog-pickup-{Guid.NewGuid():N}");
        try
        {
            var sender = new DevelopmentPickupEmailSender(directory, TimeProvider.System);
            var outcome = await sender.SendAsync(
                new EmailMessage("person@example.org", "ReFrog verification code", "Your ReFrog verification code is 012345."),
                CancellationToken.None);

            Assert.AreEqual(EmailDeliveryOutcome.Accepted, outcome);
            var file = Directory.GetFiles(directory).Single();
            var content = await File.ReadAllTextAsync(file);
            StringAssert.Contains(content, "To: person@example.org");
            StringAssert.Contains(content, "012345");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    [DataRow("Development", null, typeof(DevelopmentPickupEmailSender))]
    [DataRow("Production", null, typeof(UnavailableEmailSender))]
    [DataRow("Development", "MicrosoftGraph", typeof(MicrosoftGraphEmailSender))]
    public async Task PickupSenderIsUsedOnlyInDevelopmentWithoutAProvider(string environment, string? provider, Type expected)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Email:DevelopmentPickupDirectory"] = Path.GetTempPath(),
            ["Email:Provider"] = provider,
            ["Email:MicrosoftGraph:TenantId"] = Guid.NewGuid().ToString(),
            ["Email:MicrosoftGraph:ClientId"] = Guid.NewGuid().ToString(),
            ["Email:MicrosoftGraph:ClientSecret"] = "test-only",
            ["Email:MicrosoftGraph:SenderAddress"] = "sender@example.org",
        };
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment(environment);
            host.UseSetting("Email:Provider", provider);
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        });
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.IsInstanceOfType(scope.ServiceProvider.GetRequiredService<IEmailSender>(), expected);
    }
}
