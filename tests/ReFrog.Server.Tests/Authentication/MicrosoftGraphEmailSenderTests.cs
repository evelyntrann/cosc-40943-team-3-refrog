using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class MicrosoftGraphEmailSenderTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:MicrosoftGraph:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Email:MicrosoftGraph:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Email:MicrosoftGraph:ClientSecret"] = "test-credential",
            ["Email:MicrosoftGraph:SenderAddress"] = "refrog@example.org",
        }).Build();

    [TestMethod]
    public async Task AcceptedGraphRequestUsesConfiguredMailboxAndPlainTextMessage()
    {
        var requests = new List<(Uri Uri, string? Authorization, string Body)>();
        using var client = new HttpClient(new StubHandler(async request =>
        {
            requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString(),
                await request.Content!.ReadAsStringAsync()));
            return requests.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"test-access-token\"}"),
                }
                : new HttpResponseMessage(HttpStatusCode.Accepted);
        }));
        var sender = CreateSender(client);

        var result = await sender.SendAsync(new EmailMessage("recipient@example.org", "Verify ReFrog", "Code 123456"),
            CancellationToken.None);

        Assert.AreEqual(EmailDeliveryOutcome.Accepted, result);
        Assert.HasCount(2, requests);
        Assert.AreEqual("https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/oauth2/v2.0/token",
            requests[0].Uri.ToString());
        StringAssert.Contains(requests[0].Body, "grant_type=client_credentials");
        Assert.AreEqual("Bearer test-access-token", requests[1].Authorization);
        Assert.AreEqual("https://graph.microsoft.com/v1.0/users/refrog%40example.org/sendMail",
            requests[1].Uri.ToString());
        using var body = JsonDocument.Parse(requests[1].Body);
        var mail = body.RootElement.GetProperty("message");
        Assert.AreEqual("Text", mail.GetProperty("body").GetProperty("contentType").GetString());
        Assert.AreEqual("Code 123456", mail.GetProperty("body").GetProperty("content").GetString());
        Assert.AreEqual("recipient@example.org", mail.GetProperty("toRecipients")[0]
            .GetProperty("emailAddress").GetProperty("address").GetString());
    }

    [TestMethod]
    public async Task RejectedTokenNeverAttemptsMail()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }));

        var result = await CreateSender(client)
            .SendAsync(new EmailMessage("recipient@example.org", "Verify", "Code"), CancellationToken.None);

        Assert.AreEqual(EmailDeliveryOutcome.Failed, result);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task AmbiguousGraphFailureIsNotReportedAsAcceptedOrRetried()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return Task.FromResult(calls == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"test-access-token\"}"),
                }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }));

        var result = await CreateSender(client)
            .SendAsync(new EmailMessage("recipient@example.org", "Verify", "Code"), CancellationToken.None);

        Assert.AreEqual(EmailDeliveryOutcome.Unknown, result);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void MissingConfigurationFailsWithoutExposingItsValues()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MicrosoftGraphEmailSender(client, new ConfigurationBuilder().Build(),
                NullLogger<MicrosoftGraphEmailSender>.Instance));

        Assert.AreEqual("Microsoft Graph email configuration is incomplete.", error.Message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private static MicrosoftGraphEmailSender CreateSender(HttpClient client) =>
        new(client, Configuration, NullLogger<MicrosoftGraphEmailSender>.Instance);
}
