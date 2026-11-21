using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Server.Features.Authentication;

public sealed record RecoveryReply<T>(int StatusCode, ApiResponse<T> Response);

public sealed class RecoveryService(
    IRecoveryRepository repository,
    ILookupNormalizer normalizer,
    IEmailSender sender,
    IConfiguration configuration,
    ILogger<RecoveryService> logger)
{
    private static readonly Action<ILogger, Exception?> RequestUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7201, "RecoveryRequestUnavailable"),
            "Account recovery request could not be completed");
    private static readonly Action<ILogger, Exception?> DeliveryUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7202, "RecoveryDeliveryUnavailable"),
            "Account recovery delivery could not be confirmed");
    private static readonly Action<ILogger, Exception?> ResetUnavailable =
        LoggerMessage.Define(LogLevel.Error, new EventId(7203, "PasswordResetUnavailable"),
            "Password reset could not be recorded");

    public async Task<RecoveryReply<RecoveryAcknowledgement>> RequestAsync(
        RequestRecoveryRequest request, CancellationToken cancellationToken)
    {
        if (!ValidEmail(request.Email))
        {
            return Failure<RecoveryAcknowledgement>(422, ApiErrorCategory.Validation, "VALIDATION_FAILED",
                "Enter a valid email address.", new Dictionary<string, string> { ["email"] = "Enter a valid email address." });
        }

        try
        {
            await repository.RequestAsync(normalizer.NormalizeEmail(request.Email.Trim()), DeliverAsync, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            RequestUnavailable(logger, null);
        }

        return new(202, ApiResponse.Success(new RecoveryAcknowledgement(
            "If an account matches that email, a password-reset link will be sent. You can request another link if needed.")));
    }

    public async Task<RecoveryReply<PasswordResetResponse>> ResetAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return InvalidToken();
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return Failure<PasswordResetResponse>(422, ApiErrorCategory.Validation, "VALIDATION_FAILED",
                "Correct the invalid fields.", new Dictionary<string, string>
                {
                    ["confirmPassword"] = "Passwords must match.",
                });
        }

        try
        {
            var outcome = await repository.ResetAsync(normalizer.NormalizeEmail(request.Email.Trim()),
                request.Token, request.NewPassword ?? string.Empty, cancellationToken);
            return outcome switch
            {
                PasswordResetOutcome.Saved => new(200, ApiResponse.Success(new PasswordResetResponse(true))),
                PasswordResetOutcome.InvalidToken => InvalidToken(),
                PasswordResetOutcome.InvalidPassword => Failure<PasswordResetResponse>(422, ApiErrorCategory.Validation,
                    "VALIDATION_FAILED", "Correct the invalid fields.", new Dictionary<string, string>
                    {
                        ["newPassword"] = "Use at least six characters, including uppercase, lowercase, a number, and a symbol.",
                    }),
                _ => NotRecorded(),
            };
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            ResetUnavailable(logger, null);
            return NotRecorded();
        }
    }

    private async Task<EmailDeliveryOutcome> DeliverAsync(string email, string token, CancellationToken cancellationToken)
    {
        var configuredBase = configuration["Authentication:PublicBaseUrl"];
        if (!Uri.TryCreate(configuredBase, UriKind.Absolute, out var publicBase) ||
            publicBase.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(publicBase.UserInfo) ||
            !string.IsNullOrEmpty(publicBase.Query) || !string.IsNullOrEmpty(publicBase.Fragment))
        {
            DeliveryUnavailable(logger, null);
            return EmailDeliveryOutcome.Failed;
        }

        var resetPage = new Uri(new Uri(publicBase.AbsoluteUri.TrimEnd('/') + "/"), "reset-password");
        var link = $"{resetPage.AbsoluteUri}#email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        try
        {
            var outcome = await sender.SendAsync(new EmailMessage(email, "Reset your ReFrog password",
                $"Open this link to reset your password. The link expires in 30 minutes.\n\n{link}"), cancellationToken);
            if (outcome != EmailDeliveryOutcome.Accepted)
            {
                DeliveryUnavailable(logger, null);
            }

            return outcome;
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            DeliveryUnavailable(logger, null);
            return EmailDeliveryOutcome.Unknown;
        }
    }

    private static bool ValidEmail(string? email) =>
        MailAddress.TryCreate(email?.Trim(), out var address) && address.Address == email?.Trim() && address.Address.Length <= 256;

    private static RecoveryReply<PasswordResetResponse> InvalidToken() =>
        Failure<PasswordResetResponse>(422, ApiErrorCategory.Validation, "RESET_TOKEN_INVALID",
            "This reset link is invalid or has expired. Request a new recovery link.");

    private static RecoveryReply<PasswordResetResponse> NotRecorded() =>
        Failure<PasswordResetResponse>(503, ApiErrorCategory.Unavailable, "NOT_RECORDED",
            "The password change was not recorded. Try again while your reset link is valid.");

    private static RecoveryReply<T> Failure<T>(int statusCode, ApiErrorCategory category, string code, string message,
        IReadOnlyDictionary<string, string>? fields = null) =>
        new(statusCode, ApiResponse.Failure<T>(new ApiError(category, code, message, fields)));
}
