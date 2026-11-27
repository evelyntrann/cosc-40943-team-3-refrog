using Microsoft.AspNetCore.Identity;

namespace ReFrog.Server.Features.Authentication;

/// <summary>The purposes a verification challenge can serve (AUTH.md "Shared challenge service contract").</summary>
public static class VerificationPurposes
{
    public const string AccountRegistration = "ACCOUNT_REGISTRATION";
    public const string TcuAffiliation = "TCU_AFFILIATION";
}

/// <summary>
/// Who a challenge belongs to: a pending registration for <c>ACCOUNT_REGISTRATION</c>, an account for
/// <c>TCU_AFFILIATION</c>. Limits count by owner (AUTH.md "Limits by owner").
/// </summary>
public readonly record struct ChallengeOwner(string Purpose, Guid Id)
{
    public static ChallengeOwner Registration(Guid pendingRegistrationId) =>
        new(VerificationPurposes.AccountRegistration, pendingRegistrationId);

    public static ChallengeOwner Affiliation(Guid accountId) =>
        new(VerificationPurposes.TcuAffiliation, accountId);

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
public sealed record CodeCheck(CodeCheckOutcome Outcome, DateTimeOffset At, DateTimeOffset? RetryAt = null,
    string? NormalizedEmail = null);

/// <summary>What happened when a committed challenge's code was sent.</summary>
public sealed record ChallengeDelivery(EmailDeliveryOutcome Outcome, bool Recorded);

/// <summary>
/// Outcomes of a <c>TCU_AFFILIATION</c> code request. The three failures stay distinct
/// (AUTH.md "Results, not HTTP"): nothing was written, the write did not commit, or the send is not confirmed.
/// </summary>
public enum ChallengeRequestOutcome
{
    Sent,
    InvalidEmail,
    AlreadyVerified,
    RequestIdReused,
    Limited,
    StateUnavailable,
    NotRecorded,
    SendNotConfirmed,
}

public sealed record ChallengeRequestResult(
    ChallengeRequestOutcome Outcome,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? ResendAvailableAt = null,
    DateTimeOffset? RetryAt = null);

/// <summary>
/// What the store did with an affiliation request inside its serializable transaction. A committed new code is
/// <see cref="ChallengeRequestOutcome.Sent"/> with <c>Issued</c> set and not yet delivered.
/// A same-requestId replay sets <c>Replayed</c> and carries the first send's recorded outcome, which may be empty.
/// </summary>
public sealed record StoredChallengeRequest(
    ChallengeRequestOutcome Outcome,
    IssuedCode? Issued = null,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? ResendAvailableAt = null,
    DateTimeOffset? RetryAt = null,
    bool Replayed = false,
    string? ReplayedSendOutcome = null);

public enum ChallengeVerifyOutcome
{
    Verified,
    InvalidCode,
    ExpiredCode,
    Limited,
    StateUnavailable,
    NotRecorded,
}

public sealed record ChallengeVerifyResult(
    ChallengeVerifyOutcome Outcome,
    DateTimeOffset? VerifiedAt = null,
    DateTimeOffset? RetryAt = null);

public interface IVerificationChallengeStore
{
    Task<bool> RecordSendOutcomeAsync(Guid challengeId, EmailDeliveryOutcome outcome, CancellationToken cancellationToken);

    Task<StoredChallengeRequest> RequestAffiliationAsync(Guid accountId, string normalizedEmail, Guid requestId,
        CancellationToken cancellationToken);

    Task<ChallengeVerifyResult> VerifyAffiliationAsync(Guid accountId, string code, CancellationToken cancellationToken);
}

/// <summary>
/// The AUTH-owned verification-code workflow shared by every purpose (AUTH.md <c>VerificationChallengeService</c>).
/// The challenge is committed before this sends it; the delivery outcome is recorded afterwards
/// (AUTH.md "Send after challenge commit").
/// </summary>
public sealed class VerificationChallengeService(
    IVerificationChallengeStore store,
    IEmailSender emailSender,
    ILookupNormalizer normalizer,
    ILogger<VerificationChallengeService> logger)
{
    private static readonly Action<ILogger, Exception?> SendNotConfirmed =
        LoggerMessage.Define(LogLevel.Error, new EventId(7911, "AffiliationCodeSendNotConfirmed"),
            "TCU affiliation code delivery was not confirmed");

    private static readonly Action<ILogger, Exception?> OutcomeNotRecorded =
        LoggerMessage.Define(LogLevel.Error, new EventId(7912, "AffiliationCodeOutcomeNotRecorded"),
            "TCU affiliation code delivery outcome could not be recorded");

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

    /// <summary>
    /// Requests a <c>TCU_AFFILIATION</c> code for the signed-in account. The address is checked once here
    /// (<c>BR-tcu-email-domain</c>) before anything is written; normalization is AUTH's Identity normalizer.
    /// </summary>
    public async Task<ChallengeRequestResult> RequestTcuAffiliationAsync(Guid accountId, string? email, Guid requestId,
        CancellationToken cancellationToken)
    {
        var trimmed = email?.Trim() ?? string.Empty;
        if (!TcuEmailAddress.IsAcceptable(trimmed))
        {
            return new(ChallengeRequestOutcome.InvalidEmail);
        }

        var stored = await store.RequestAffiliationAsync(accountId, normalizer.NormalizeEmail(trimmed), requestId,
            cancellationToken);
        if (stored.Replayed)
        {
            // A same-requestId retry: only a recorded acceptance is ever reported as sent.
            return stored.ReplayedSendOutcome == "ACCEPTED"
                ? new(ChallengeRequestOutcome.Sent, stored.ExpiresAt, stored.ResendAvailableAt)
                : new(ChallengeRequestOutcome.SendNotConfirmed);
        }

        if (stored.Issued is not { } issued)
        {
            return new(stored.Outcome, RetryAt: stored.RetryAt);
        }

        ChallengeDelivery delivery;
        try
        {
            delivery = await DeliverAsync(trimmed, issued, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            OutcomeNotRecorded(logger, null);
            return new(ChallengeRequestOutcome.SendNotConfirmed);
        }

        if (!delivery.Recorded || delivery.Outcome != EmailDeliveryOutcome.Accepted)
        {
            SendNotConfirmed(logger, null);
            return new(ChallengeRequestOutcome.SendNotConfirmed);
        }

        return new(ChallengeRequestOutcome.Sent, issued.ExpiresAt, issued.ResendAvailableAt);
    }

    /// <summary>
    /// Verifies a submitted <c>TCU_AFFILIATION</c> code. A match is consumed and the account's TCU verification is
    /// written in one transaction (AUTH.md "Affiliation verification writes the record").
    /// </summary>
    public Task<ChallengeVerifyResult> VerifyTcuAffiliationAsync(Guid accountId, string code, CancellationToken cancellationToken) =>
        store.VerifyAffiliationAsync(accountId, code, cancellationToken);
}
