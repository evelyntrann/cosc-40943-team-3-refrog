namespace ReFrog.Server.Features.Authentication;

/// <summary>The purposes a verification challenge can serve (AUTH.md "Shared challenge service contract").</summary>
public static class VerificationPurposes
{
    public const string AccountRegistration = "ACCOUNT_REGISTRATION";
    public const string TcuAffiliation = "TCU_AFFILIATION";
}

/// <summary>
/// Who a challenge belongs to: a pending registration for <c>ACCOUNT_REGISTRATION</c>.
/// Limits count by owner (AUTH.md "Limits by owner").
/// </summary>
public readonly record struct ChallengeOwner(string Purpose, Guid Id)
{
    public static ChallengeOwner Registration(Guid pendingRegistrationId) =>
        new(VerificationPurposes.AccountRegistration, pendingRegistrationId);

    public bool IsRegistration => Purpose == VerificationPurposes.AccountRegistration;
}

/// <summary>
/// The single set of code values, from <c>FR-AUTH-verification-code</c> and <c>FR-AUTH-verification-limits</c>.
/// Callers set none of them.
/// </summary>
public static class VerificationPolicy
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendSpacing = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);
    public const int MaxRequestsPerWindow = 5;
    public const int MaxWrongPerCode = 5;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    public const int MaxRecentFailures = 10;
}

/// <summary>A challenge written in the current transaction, not yet sent.</summary>
public sealed record IssuedCode(Guid ChallengeId, string Code, DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt);

/// <summary>Either a refusal with its retry time, or a newly issued code.</summary>
public sealed record IssueResult(DateTimeOffset? RetryAt, IssuedCode? Issued);

public enum CodeCheckOutcome
{
    Matched,
    Invalid,
    Expired,
    Limited,
    KeyRetired,
    LostRace,
}

/// <summary>The result of checking a submitted code against the owner's live challenge.</summary>
public sealed record CodeCheck(CodeCheckOutcome Outcome, DateTimeOffset At, DateTimeOffset? RetryAt = null);

/// <summary>What happened when a committed challenge's code was sent.</summary>
public sealed record ChallengeDelivery(EmailDeliveryOutcome Outcome, bool Recorded);

public interface IVerificationChallengeStore
{
    Task<bool> RecordSendOutcomeAsync(Guid challengeId, EmailDeliveryOutcome outcome, CancellationToken cancellationToken);
}

/// <summary>
/// The AUTH-owned verification-code workflow shared by every purpose (AUTH.md <c>VerificationChallengeService</c>).
/// The challenge is committed before this sends it; the delivery outcome is recorded afterwards
/// (AUTH.md "Send after challenge commit").
/// </summary>
public sealed class VerificationChallengeService(IVerificationChallengeStore store, IEmailSender emailSender)
{
    public async Task<ChallengeDelivery> DeliverAsync(string destination, IssuedCode issued, CancellationToken cancellationToken)
    {
        EmailDeliveryOutcome delivery;
        try
        {
            delivery = await emailSender.SendAsync(
                new EmailMessage(destination, "ReFrog verification code", $"Your ReFrog verification code is {issued.Code}."),
                cancellationToken);
        }
        catch (Exception)
        {
            delivery = EmailDeliveryOutcome.Unknown;
        }

        var recorded = await store.RecordSendOutcomeAsync(issued.ChallengeId, delivery, cancellationToken);
        return new(delivery, recorded);
    }
}
