using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class SmtpEmailSenderTests
{
    [TestMethod]
    [TestCategory("LocalIntegration")]
    public async Task SendsMessageToMailpitWhenConfigured()
    {
        var host = Environment.GetEnvironmentVariable("MAILPIT_SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            Assert.Inconclusive("Set MAILPIT_SMTP_HOST to run the local Mailpit integration test.");
        }

        var port = Environment.GetEnvironmentVariable("MAILPIT_SMTP_PORT") ?? "1025";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = host,
            ["Email:Smtp:Port"] = port,
            ["Email:Smtp:SenderAddress"] = "refrog@localhost.test",
            ["Email:Smtp:UseTls"] = "false",
        }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance);

        var outcome = await sender.SendAsync(
            new EmailMessage("mailpit-test@localhost.test", "ReFrog Mailpit test",
                "The ReFrog SMTP adapter successfully reached Mailpit."),
            CancellationToken.None);

        Assert.AreEqual(EmailDeliveryOutcome.Accepted, outcome);
    }

    [TestMethod]
    public async Task SendsMessageThroughConfiguredSmtpServer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var conversation = new List<string>();
        var server = RunSmtpServerAsync(listener, conversation);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "127.0.0.1",
            ["Email:Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Email:Smtp:SenderAddress"] = "refrog@localhost.test",
            ["Email:Smtp:UseTls"] = "false",
        }).Build();
        var sender = new SmtpEmailSender(configuration, NullLogger<SmtpEmailSender>.Instance);

        var outcome = await sender.SendAsync(
            new EmailMessage("person@example.org", "ReFrog verification", "Verification code: 012345"),
            CancellationToken.None);
        await server;

        Assert.AreEqual(EmailDeliveryOutcome.Accepted, outcome);
        Assert.IsTrue(conversation.Any(line => line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(conversation.Any(line => line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(conversation.Any(line => line.Contains("Verification code: 012345", StringComparison.Ordinal)));
    }

    private static async Task RunSmtpServerAsync(TcpListener listener, List<string> conversation)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost Mailpit test SMTP");

            var readingData = false;
            while (await reader.ReadLineAsync() is { } line)
            {
                conversation.Add(line);
                if (readingData)
                {
                    if (line == ".")
                    {
                        readingData = false;
                        await writer.WriteLineAsync("250 accepted");
                    }

                    continue;
                }

                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 localhost");
                }
                else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 accepted");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    readingData = true;
                    await writer.WriteLineAsync("354 end with dot");
                }
                else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 accepted");
                }
            }
        }
        finally
        {
            listener.Stop();
        }
    }
}
