namespace ReFrog.Server.Features.Authentication;

public interface IEmailSender
{
    Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
