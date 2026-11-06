using System.Globalization;

namespace ReFrog.Server.Features.Authentication;

/// <summary>
/// Development-only stand-in for the unchosen email provider: writes each message to a local
/// folder so the registration flow can be run end to end. Codes go to the file, never to logs
/// (architecture §8.2.7). Program registers it only in the Development environment.
/// </summary>
public sealed class DevelopmentPickupEmailSender(string directory, TimeProvider clock) : IEmailSender
{
    public async Task<EmailDeliveryOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Directory.CreateDirectory(directory);
        var name = string.Create(CultureInfo.InvariantCulture,
            $"{clock.GetUtcNow():yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}.txt");
        var content = $"To: {message.Destination}\nSubject: {message.Subject}\n\n{message.PlainTextBody}\n";
        await File.WriteAllTextAsync(Path.Combine(directory, name), content, cancellationToken);
        return EmailDeliveryOutcome.Accepted;
    }
}
