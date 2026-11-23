using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;
using ReFrog.Server.Features.Authentication;

namespace ReFrog.Server.Features.IdentityVerification;

public enum ShoppingEligibilityKind
{
    Eligible,
    NotEligible,
    Restricted,
}

/// <summary>What Shopping gets from <see cref="AffiliationService.CheckShoppingEligibilityAsync"/>.</summary>
public sealed record ShoppingEligibility(ShoppingEligibilityKind Kind, string? Basis = null);

public sealed record AffiliationReply<T>(int StatusCode, ApiResponse<T> Response);

/// <summary>
/// Shopping eligibility for <c>UC-IDV-verify-affiliation</c> (IDV.md <c>AffiliationService</c>): current restriction
/// first, then active volunteer access, then persisted TCU email verification. Code requests and submissions go to
/// Authentication's shared challenge service; this class repeats none of its rules.
/// An unreadable access or verification state throws <see cref="AccessStateUnavailableException"/> (step 2d).
/// </summary>
public sealed class AffiliationService(
    ICurrentUser currentUser,
    ITcuVerificationReader reader,
    VerificationChallengeService challenges)
{
    public async Task<ShoppingEligibilityResponse> StatusAsync(Guid accountId, VolunteerAccessState access,
        CancellationToken cancellationToken)
    {
        var verification = await reader.ForAccountAsync(accountId, cancellationToken);
        var (eligible, basis) = Decide(access, verification.Verified);
        return new(eligible, basis, verification.Verified, Wire(access), verification.PendingCodeExpiresAt);
    }

    public async Task<AffiliationReply<TcuCodeResponse>> RequestCodeAsync(Guid accountId, VolunteerAccessState access,
        RequestTcuCodeRequest request, CancellationToken cancellationToken)
    {
        if (access == VolunteerAccessState.Restricted)
        {
            return Inactive<TcuCodeResponse>();
        }

        if ((await reader.ForAccountAsync(accountId, cancellationToken)).Verified)
        {
            return AlreadyVerified();
        }

        if (request.RequestId == Guid.Empty)
        {
            return Invalid<TcuCodeResponse>("requestId", "Start a new code request.");
        }

        var result = await challenges.RequestTcuAffiliationAsync(accountId, request.Email, request.RequestId, cancellationToken);
        return result.Outcome switch
        {
            ChallengeRequestOutcome.Sent => new(202, ApiResponse.Success(
                new TcuCodeResponse(result.ExpiresAt!.Value, result.ResendAvailableAt!.Value))),
            ChallengeRequestOutcome.InvalidEmail => Invalid<TcuCodeResponse>("email", "Enter a TCU email address ending in @tcu.edu."),
            ChallengeRequestOutcome.AlreadyVerified => AlreadyVerified(),
            ChallengeRequestOutcome.RequestIdReused => Error<TcuCodeResponse>(409, ApiErrorCategory.Validation,
                "REQUEST_ID_REUSED", "Start a new code request."),
            ChallengeRequestOutcome.Limited => Limited<TcuCodeResponse>(result.RetryAt),
            ChallengeRequestOutcome.NotRecorded => NotRecorded<TcuCodeResponse>(),
            ChallengeRequestOutcome.SendNotConfirmed => Error<TcuCodeResponse>(503, ApiErrorCategory.Unavailable,
                "EMAIL_NOT_SENT", "The code email could not be confirmed. Try again when allowed."),
            _ => AccessUnavailable<TcuCodeResponse>(),
        };
    }

    public async Task<AffiliationReply<TcuVerificationResponse>> VerifyCodeAsync(Guid accountId, VolunteerAccessState access,
        VerifyTcuCodeRequest request, CancellationToken cancellationToken)
    {
        if (access == VolunteerAccessState.Restricted)
        {
            return Inactive<TcuVerificationResponse>();
        }

        var code = request.Code?.Trim();
        if (code is null || code.Length != 6 || !code.All(char.IsAsciiDigit))
        {
            return Invalid<TcuVerificationResponse>("code", "Enter the six-digit code.");
        }

        // Already verified, for example in another tab: answer with the saved verification instead of
        // "send a new code", which would only lead to 409 ALREADY_VERIFIED (code review of PR #149, finding 8).
        // Nothing is written and no failure is counted.
        var saved = await reader.ForAccountAsync(accountId, cancellationToken);
        if (saved is { Verified: true, VerifiedAt: { } verifiedAt })
        {
            var (savedEligible, savedBasis) = Decide(access, tcuEmailVerified: true);
            return new(200, ApiResponse.Success(new TcuVerificationResponse(
                savedEligible, savedBasis, true, Wire(access), null, verifiedAt)));
        }

        var result = await challenges.VerifyTcuAffiliationAsync(accountId, code, cancellationToken);
        switch (result.Outcome)
        {
            case ChallengeVerifyOutcome.Verified:
                // Built from the committed result, so a failed re-read can never turn a saved verification into an error.
                var (eligible, basis) = Decide(access, tcuEmailVerified: true);
                return new(200, ApiResponse.Success(new TcuVerificationResponse(
                    eligible, basis, true, Wire(access), null, result.VerifiedAt!.Value)));
            case ChallengeVerifyOutcome.InvalidCode:
                return Error<TcuVerificationResponse>(422, ApiErrorCategory.Validation, "CODE_INVALID",
                    "That code is not correct. Try again or send a new code.");
            case ChallengeVerifyOutcome.ExpiredCode:
                return Error<TcuVerificationResponse>(422, ApiErrorCategory.Validation, "CODE_EXPIRED",
                    "That code is no longer valid. Send a new code.");
            case ChallengeVerifyOutcome.Limited:
                return Limited<TcuVerificationResponse>(result.RetryAt);
            case ChallengeVerifyOutcome.NotRecorded:
                return NotRecorded<TcuVerificationResponse>();
            default:
                return AccessUnavailable<TcuVerificationResponse>();
        }
    }

    /// <summary>
    /// The check Shopping calls on every protected submission (<c>UC-SHP-log-item-taken</c> PRE-1), in step 2's order.
    /// It throws <see cref="AccessStateUnavailableException"/> rather than answer "not eligible" for an unreadable state.
    /// </summary>
    public async Task<ShoppingEligibility> CheckShoppingEligibilityAsync(CancellationToken cancellationToken)
    {
        var accountId = currentUser.AccountId ?? throw new InvalidOperationException("No signed-in account.");
        var access = await currentUser.CurrentVolunteerAccessAsync(cancellationToken);
        if (access == VolunteerAccessState.Restricted)
        {
            return new(ShoppingEligibilityKind.Restricted);
        }

        if (access == VolunteerAccessState.RoleActive)
        {
            return new(ShoppingEligibilityKind.Eligible, EligibilityBasis.VolunteerRole);
        }

        return (await reader.ForAccountAsync(accountId, cancellationToken)).Verified
            ? new(ShoppingEligibilityKind.Eligible, EligibilityBasis.TcuEmail)
            : new(ShoppingEligibilityKind.NotEligible);
    }

    private static (bool Eligible, string? Basis) Decide(VolunteerAccessState access, bool tcuEmailVerified) => access switch
    {
        VolunteerAccessState.Restricted => (false, null),
        VolunteerAccessState.RoleActive => (true, EligibilityBasis.VolunteerRole),
        _ => tcuEmailVerified ? (true, EligibilityBasis.TcuEmail) : (false, null),
    };

    private static string Wire(VolunteerAccessState access) => access switch
    {
        VolunteerAccessState.RoleActive => VolunteerAccessValues.RoleActive,
        VolunteerAccessState.Restricted => VolunteerAccessValues.Restricted,
        _ => VolunteerAccessValues.None,
    };

    private static AffiliationReply<T> Inactive<T>() => Error<T>(403, ApiErrorCategory.Authorization,
        "VOLUNTEER_ACCESS_INACTIVE", "Volunteer access inactive—administrator review required.");

    private static AffiliationReply<TcuCodeResponse> AlreadyVerified() => Error<TcuCodeResponse>(409,
        ApiErrorCategory.Validation, "ALREADY_VERIFIED", "Your TCU email is already verified.");

    private static AffiliationReply<T> Invalid<T>(string field, string message) => Error<T>(422,
        ApiErrorCategory.Validation, "VALIDATION_FAILED", "Correct the invalid fields.",
        new Dictionary<string, string> { [field] = message });

    private static AffiliationReply<T> Limited<T>(DateTimeOffset? retryAt) => Error<T>(429, ApiErrorCategory.Validation,
        "TOO_MANY_REQUESTS", "Try again after the stated time.", retryAt: retryAt);

    private static AffiliationReply<T> NotRecorded<T>() => Error<T>(503, ApiErrorCategory.Unavailable,
        "NOT_RECORDED", "Nothing was saved. Try again.");

    private static AffiliationReply<T> AccessUnavailable<T>() => Error<T>(503, ApiErrorCategory.Unavailable,
        "ACCESS_UNAVAILABLE", "Unable to verify access. Try again.");

    private static AffiliationReply<T> Error<T>(int status, ApiErrorCategory category, string code, string message,
        IReadOnlyDictionary<string, string>? fields = null, DateTimeOffset? retryAt = null) =>
        new(status, ApiResponse.Failure<T>(new(category, code, message, fields, retryAt)));
}
