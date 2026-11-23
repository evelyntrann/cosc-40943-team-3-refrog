using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Client.Features.Administration;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;

namespace ReFrog.Client.Features.IdentityVerification;

/// <summary>
/// State and server calls behind <c>AffiliationSection</c> (<c>UC-IDV-verify-affiliation</c>, IDV.md components).
/// The server decides eligibility and validates the address; this class repeats no business rule (§8.2.4).
/// A request id is kept for one explicit send and reused only when no server answer arrived.
/// </summary>
public sealed class AffiliationForm(HttpClient http, TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private Guid? sendRequestId;
    private string? sendEmail;

    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>The server's current answer, or null while loading or when it could not be read (never a stale one).</summary>
    public ShoppingEligibilityResponse? Status { get; private set; }

    /// <summary>Step 2d: the status could not be read; the view offers retry and shows no earlier status.</summary>
    public bool StatusUnavailable { get; private set; }

    public bool SignedOut { get; private set; }

    public TcuCodeResponse? Pending { get; private set; }

    public ApiError? Error { get; private set; }

    public bool Busy { get; private set; }

    public bool Restricted => Status?.VolunteerAccess == VolunteerAccessValues.Restricted;

    public bool IsVolunteer => Status?.VolunteerAccess == VolunteerAccessValues.RoleActive;

    /// <summary>"TCU email verified" is never shown for a restricted account (BR-volunteer-role-lifecycle).</summary>
    public bool ShowsTcuVerified => Status is { TcuEmailVerified: true } && !Restricted;

    public bool OffersVerification => Status is { Eligible: false } && !Restricted;

    /// <summary>
    /// A code form is shown while a code sent in this visit, or an earlier one, is still live. The email form stays
    /// visible beside it, so a mistyped address can be corrected and sent again.
    /// </summary>
    public bool AwaitingCode => OffersVerification && CodeExpiresAt is { } expiresAt && time.GetUtcNow() < expiresAt;

    public DateTimeOffset? CodeExpiresAt => Pending?.ExpiresAt ?? Status?.PendingCodeExpiresAt;

    /// <summary>True once the server's <c>resendAvailableAt</c> has passed; the wait itself comes from the server.</summary>
    public bool CanResend => Pending is null || time.GetUtcNow() >= Pending.ResendAvailableAt;

    public string? FieldError(string name) =>
        Error?.Fields?.TryGetValue(name, out var message) == true ? message : null;

    public async Task LoadStatusAsync()
    {
        Busy = true;
        Status = null;
        StatusUnavailable = false;
        try
        {
            using var response = await http.GetAsync(new Uri("api/v1/me/shopping-eligibility", UriKind.Relative));
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                SignedOut = true;
                return;
            }

            var answer = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingEligibilityResponse>>()
                : null;
            Status = answer?.Data;
            StatusUnavailable = Status is null;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            StatusUnavailable = true;
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task RequestCodeAsync()
    {
        if (!CanResend)
        {
            return;
        }

        if (sendRequestId is null || sendEmail != Email)
        {
            sendRequestId = Guid.NewGuid();
            sendEmail = Email;
        }

        var (status, answer) = await PostAsync<TcuCodeResponse>("api/v1/me/tcu-email/codes",
            new RequestTcuCodeRequest(Email, sendRequestId.Value));
        if (answer is null)
        {
            return;
        }

        sendRequestId = null;
        Error = answer.Error;
        if (answer.Data is not null)
        {
            Pending = answer.Data;
            Code = string.Empty;
        }
        else if (status == HttpStatusCode.Conflict && answer.Error?.Code == "ALREADY_VERIFIED")
        {
            await LoadStatusAsync();
        }
    }

    public async Task SubmitCodeAsync()
    {
        var (status, answer) = await PostAsync<TcuVerificationResponse>("api/v1/me/tcu-email/verification",
            new VerifyTcuCodeRequest(Code));
        if (answer?.Data is { } verified)
        {
            Status = new(verified.Eligible, verified.Basis, verified.TcuEmailVerified, verified.VolunteerAccess, null);
            Pending = null;
            Code = string.Empty;
            Error = null;
            return;
        }

        if (answer?.Error is { } error)
        {
            Error = error;
            if (error.Code is "CODE_INVALID" or "CODE_EXPIRED")
            {
                Code = string.Empty;
            }
        }

        // FR-SAVE-unknown-outcome: on a dropped response or a 5xx the outcome may be unknown, so re-read the status.
        if (answer is null || (int)status >= 500)
        {
            var kept = Error;
            await LoadStatusAsync();
            Error = kept;
        }
    }

    private async Task<(HttpStatusCode Status, ApiResponse<T>? Answer)> PostAsync<T>(string path, object body)
    {
        Busy = true;
        Error = null;
        try
        {
            var token = await AntiforgeryRequests.FetchTokenAsync(http);
            if (string.IsNullOrEmpty(token))
            {
                Error = Unavailable();
                return (0, null);
            }

            using var request = AntiforgeryRequests.Create(HttpMethod.Post, path, token, body);
            using var response = await http.SendAsync(request);
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
            if (answer is null)
            {
                Error = Unavailable();
            }

            return (response.StatusCode, answer);
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            Error = Unavailable();
            return (0, null);
        }
        finally
        {
            Busy = false;
        }
    }

    private static ApiError Unavailable() =>
        new(ApiErrorCategory.Unavailable, "NETWORK_UNAVAILABLE", "The request could not be confirmed. Try again.");
}
