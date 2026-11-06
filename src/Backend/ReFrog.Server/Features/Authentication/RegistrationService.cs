using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Features.Authentication;

public sealed record RegistrationReply<T>(int StatusCode, ApiResponse<T> Response);

public sealed class RegistrationService(
    IAuthRepository repository,
    UserManager<AuthAccount> accounts,
    SignInManager<AuthAccount> signIn,
    ILookupNormalizer normalizer,
    IEmailSender emailSender,
    TimeProvider clock,
    ILogger<RegistrationService> logger)
{
    private static readonly Action<ILogger, Exception?> PersistenceFailure =
        LoggerMessage.Define(LogLevel.Warning, new EventId(7901, "RegistrationPersistenceFailure"),
            "Registration persistence failed");

    // Matches the 256-character email columns; a longer address would pass here and fail at save.
    private const int MaxEmailLength = 256;

    public async Task<RegistrationReply<RegistrationChallengeResponse>> StartAsync(
        StartRegistrationRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        if (!MailAddress.TryCreate(request.Email?.Trim(), out var address) || address.Address != request.Email?.Trim() ||
            address.Address.Length > MaxEmailLength)
        {
            errors["email"] = "Enter a valid email address.";
        }

        var passwordValidation = await new PasswordValidator<AuthAccount>()
            .ValidateAsync(accounts, new AuthAccount(), request.Password ?? string.Empty);
        if (!passwordValidation.Succeeded)
        {
            errors["password"] = "Use at least six characters, including uppercase, lowercase, a number, and a symbol.";
        }

        if (request.RequestId == Guid.Empty)
        {
            errors["requestId"] = "Start a new registration request.";
        }

        if (errors.Count > 0)
        {
            return Error<RegistrationChallengeResponse>(422, ApiErrorCategory.Validation,
                "VALIDATION_FAILED", "Correct the invalid fields.", errors);
        }

        var normalized = normalizer.NormalizeEmail(address!.Address);
        try
        {
            var issued = await repository.StartAsync(address.Address, normalized, request.Password!, request.RequestId, cancellationToken);
            return await CompleteIssueAsync(issued, cancellationToken);
        }
        catch (Exception failure) when (IsPersistenceFailure(failure))
        {
            PersistenceFailure(logger, null);
            return Unavailable<RegistrationChallengeResponse>();
        }
    }

    public async Task<RegistrationReply<RegistrationChallengeResponse>> ResendAsync(
        Guid registrationId, ResendRegistrationCodeRequest request, CancellationToken cancellationToken)
    {
        if (request.RequestId == Guid.Empty)
        {
            return Error<RegistrationChallengeResponse>(422, ApiErrorCategory.Validation,
                "VALIDATION_FAILED", "Correct the invalid fields.",
                new Dictionary<string, string> { ["requestId"] = "Start a new code request." });
        }

        try
        {
            var issued = await repository.ResendAsync(registrationId, request.RequestId, cancellationToken);
            return await CompleteIssueAsync(issued, cancellationToken);
        }
        catch (Exception failure) when (IsPersistenceFailure(failure))
        {
            PersistenceFailure(logger, null);
            return Unavailable<RegistrationChallengeResponse>();
        }
    }

    public async Task<RegistrationReply<RegisteredAccountResponse>> VerifyAsync(
        Guid registrationId, VerifyRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (request.Code is null || request.Code.Length != 6 || !request.Code.All(char.IsAsciiDigit))
        {
            return Error<RegisteredAccountResponse>(422, ApiErrorCategory.Validation,
                "VALIDATION_FAILED", "Enter the six-digit code.",
                new Dictionary<string, string> { ["code"] = "Enter the six-digit code." });
        }

        try
        {
            var result = await repository.VerifyAsync(registrationId, request.Code, cancellationToken);
            if (result.Outcome == RegistrationOutcome.Saved && result.Account is not null)
            {
                var user = await accounts.FindByIdAsync(result.Account.AccountId.ToString());
                if (user is null)
                {
                    return Unavailable<RegisteredAccountResponse>();
                }

                var expiresAt = clock.GetUtcNow().Add(result.IsAdministrator ? TimeSpan.FromHours(4) : TimeSpan.FromDays(30));
                await signIn.SignInAsync(user, new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = expiresAt,
                    AllowRefresh = result.IsAdministrator,
                });
                return new(200, ApiResponse.Success(result.Account));
            }

            return result.Outcome switch
            {
                RegistrationOutcome.NotFound => Error<RegisteredAccountResponse>(404, ApiErrorCategory.Validation,
                    "REGISTRATION_NOT_FOUND", "Registration was not found."),
                RegistrationOutcome.Completed => Error<RegisteredAccountResponse>(409, ApiErrorCategory.Validation,
                    "REGISTRATION_COMPLETED", "This registration is already complete. Sign in instead."),
                RegistrationOutcome.Limited => Error<RegisteredAccountResponse>(429, ApiErrorCategory.Validation,
                    "TOO_MANY_REQUESTS", "Try again after the stated time.", retryAt: result.RetryAt),
                RegistrationOutcome.ExpiredCode => Error<RegisteredAccountResponse>(422, ApiErrorCategory.Validation,
                    "CODE_EXPIRED", "Request a new code."),
                RegistrationOutcome.InvalidCode => Error<RegisteredAccountResponse>(422, ApiErrorCategory.Validation,
                    "CODE_INVALID", "Enter the current code."),
                _ => Unavailable<RegisteredAccountResponse>(),
            };
        }
        catch (Exception failure) when (IsPersistenceFailure(failure))
        {
            PersistenceFailure(logger, null);
            return Unavailable<RegisteredAccountResponse>();
        }
    }

    private async Task<RegistrationReply<RegistrationChallengeResponse>> CompleteIssueAsync(
        RegistrationResult result, CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case RegistrationOutcome.Issued when result.Challenge is not null && result.Email is not null && result.Code is not null && result.ChallengeId is not null:
                EmailDeliveryOutcome delivery;
                try
                {
                    delivery = await emailSender.SendAsync(
                        new EmailMessage(result.Email, "ReFrog verification code", $"Your ReFrog verification code is {result.Code}."),
                        cancellationToken);
                }
                catch (Exception)
                {
                    delivery = EmailDeliveryOutcome.Unknown;
                }

                if (!await repository.RecordSendOutcomeAsync(result.ChallengeId.Value, delivery, cancellationToken))
                {
                    return Unavailable<RegistrationChallengeResponse>();
                }
                return delivery == EmailDeliveryOutcome.Accepted
                    ? new(202, ApiResponse.Success(result.Challenge))
                    : Error<RegistrationChallengeResponse>(503, ApiErrorCategory.Unavailable,
                        "EMAIL_NOT_SENT", "The verification message could not be confirmed. Try again when allowed.");
            case RegistrationOutcome.Replayed when result.Challenge is not null:
                return result.SendOutcome == "ACCEPTED"
                    ? new(202, ApiResponse.Success(result.Challenge))
                    : Error<RegistrationChallengeResponse>(503, ApiErrorCategory.Unavailable,
                        "EMAIL_NOT_SENT", "The verification message could not be confirmed. Try again when allowed.");
            case RegistrationOutcome.DuplicateEmail:
                return Error<RegistrationChallengeResponse>(409, ApiErrorCategory.Validation,
                    "EMAIL_ALREADY_REGISTERED", "This account already exists. Sign in or recover your account.");
            case RegistrationOutcome.RequestIdReused:
                return Error<RegistrationChallengeResponse>(409, ApiErrorCategory.Validation,
                    "REQUEST_ID_REUSED", "Start a new request.");
            case RegistrationOutcome.NotFound:
                return Error<RegistrationChallengeResponse>(404, ApiErrorCategory.Validation,
                    "REGISTRATION_NOT_FOUND", "Registration was not found.");
            case RegistrationOutcome.Completed:
                return Error<RegistrationChallengeResponse>(409, ApiErrorCategory.Validation,
                    "REGISTRATION_COMPLETED", "This registration is already complete. Sign in instead.");
            case RegistrationOutcome.Limited:
                return Error<RegistrationChallengeResponse>(429, ApiErrorCategory.Validation,
                    "TOO_MANY_REQUESTS", "Try again after the stated time.", retryAt: result.RetryAt);
            default:
                return Unavailable<RegistrationChallengeResponse>();
        }
    }

    private static RegistrationReply<T> Unavailable<T>() =>
        Error<T>(503, ApiErrorCategory.Unavailable, "NOT_RECORDED", "Registration was not completed. Try again.");

    private static bool IsPersistenceFailure(Exception failure) =>
        failure is DbUpdateException or SqlException ||
        (failure is InvalidOperationException && failure.InnerException is DbUpdateException or SqlException);

    private static RegistrationReply<T> Error<T>(int status, ApiErrorCategory category, string code, string message,
        IReadOnlyDictionary<string, string>? fields = null, DateTimeOffset? retryAt = null) =>
        new(status, ApiResponse.Failure<T>(new(category, code, message, fields, retryAt)));
}
