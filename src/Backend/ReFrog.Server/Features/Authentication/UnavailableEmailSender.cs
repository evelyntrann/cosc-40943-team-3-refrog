namespace ReFrog.Server.Features.Authentication;

public sealed class UnavailableEmailSender : IEmailSender
{
    public Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(EmailDeliveryOutcome.Failed);
}
