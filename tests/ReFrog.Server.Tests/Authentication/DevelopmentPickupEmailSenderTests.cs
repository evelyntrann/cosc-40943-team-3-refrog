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
}
