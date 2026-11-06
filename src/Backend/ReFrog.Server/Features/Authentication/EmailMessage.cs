namespace ReFrog.Server.Features.Authentication;

public sealed record EmailMessage(string Destination, string Subject, string PlainTextBody);

public enum EmailDeliveryOutcome
{
    Accepted,
    Failed,
    Unknown,
}
