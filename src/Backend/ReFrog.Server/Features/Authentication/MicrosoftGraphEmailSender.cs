using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;

namespace ReFrog.Server.Features.Authentication;

public sealed class MicrosoftGraphEmailSender : IEmailSender
{
    private readonly HttpClient client;
    private readonly Guid tenantId;
    private readonly Guid clientId;
    private readonly string clientSecret;
    private readonly string senderAddress;
    private readonly ILogger<MicrosoftGraphEmailSender> logger;
    private static readonly Action<ILogger, EmailDeliveryOutcome, Exception?> DeliveryFailure =
        LoggerMessage.Define<EmailDeliveryOutcome>(LogLevel.Warning,
            new EventId(7903, "MicrosoftGraphEmailFailure"), "Microsoft Graph email outcome: {Outcome}");

    public MicrosoftGraphEmailSender(HttpClient client, IConfiguration configuration,
        ILogger<MicrosoftGraphEmailSender> logger)
    {
        this.client = client;
        this.logger = logger;
        var section = configuration.GetSection("Email:MicrosoftGraph");
        if (!Guid.TryParse(section["TenantId"], out tenantId) ||
            !Guid.TryParse(section["ClientId"], out clientId) ||
            string.IsNullOrWhiteSpace(section["ClientSecret"]) ||
            !MailAddress.TryCreate(section["SenderAddress"], out var sender) ||
            sender.Address != section["SenderAddress"])
        {
            throw new InvalidOperationException("Microsoft Graph email configuration is incomplete.");
        }

        clientSecret = section["ClientSecret"]!;
        senderAddress = sender.Address;
    }

    public async Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        string token;
        try
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://login.microsoftonline.com/{tenantId:D}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId.ToString("D"),
                    ["client_secret"] = clientSecret,
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["grant_type"] = "client_credentials",
                }),
            };
            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return Report(EmailDeliveryOutcome.Failed);
            }

            using var tokenBody = await JsonDocument.ParseAsync(
                await tokenResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!tokenBody.RootElement.TryGetProperty("access_token", out var accessToken) ||
                accessToken.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(accessToken.GetString()))
            {
                return Report(EmailDeliveryOutcome.Failed);
            }

            token = accessToken.GetString()!;
        }
        catch (HttpRequestException)
        {
            return Report(EmailDeliveryOutcome.Failed);
        }
        catch (JsonException)
        {
            return Report(EmailDeliveryOutcome.Failed);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Report(EmailDeliveryOutcome.Failed);
        }

        using var sendRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(senderAddress)}/sendMail")
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    subject = message.Subject,
                    body = new { contentType = "Text", content = message.PlainTextBody },
                    toRecipients = new[] { new { emailAddress = new { address = message.Destination } } },
                },
            }),
        };
        sendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var sendResponse = await client.SendAsync(sendRequest, cancellationToken);
            return Report(sendResponse.StatusCode switch
            {
                HttpStatusCode.Accepted => EmailDeliveryOutcome.Accepted,
                >= HttpStatusCode.InternalServerError => EmailDeliveryOutcome.Unknown,
                _ => EmailDeliveryOutcome.Failed,
            });
        }
        catch (HttpRequestException)
        {
            return Report(EmailDeliveryOutcome.Unknown);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Report(EmailDeliveryOutcome.Unknown);
        }
    }

    private EmailDeliveryOutcome Report(EmailDeliveryOutcome outcome)
    {
        if (outcome != EmailDeliveryOutcome.Accepted)
        {
            DeliveryFailure(logger, outcome, null);
        }

        return outcome;
    }
}
