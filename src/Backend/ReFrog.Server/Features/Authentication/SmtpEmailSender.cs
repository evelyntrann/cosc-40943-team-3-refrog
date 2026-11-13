using System.Net;
using System.Net.Mail;

namespace ReFrog.Server.Features.Authentication;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly string host;
    private readonly int port;
    private readonly string senderAddress;
    private readonly bool useTls;
    private readonly string? username;
    private readonly string? password;
    private readonly ILogger<SmtpEmailSender> logger;

    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(7904, "SmtpEmailFailure"), "SMTP email delivery failed");

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        var section = configuration.GetSection("Email:Smtp");
        host = section["Host"] ?? throw new InvalidOperationException("Email:Smtp:Host is required.");
        port = section.GetValue<int?>("Port") ?? 25;
        senderAddress = section["SenderAddress"] ??
            throw new InvalidOperationException("Email:Smtp:SenderAddress is required.");
        useTls = section.GetValue<bool>("UseTls");
        username = section["Username"];
        password = section["Password"];
        this.logger = logger;
    }

    public async Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var mail = new MailMessage(senderAddress, message.Destination, message.Subject, message.PlainTextBody);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(username),
        };
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
            return EmailDeliveryOutcome.Accepted;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            LogFailure(logger, exception);
            return EmailDeliveryOutcome.Failed;
        }
    }
}
