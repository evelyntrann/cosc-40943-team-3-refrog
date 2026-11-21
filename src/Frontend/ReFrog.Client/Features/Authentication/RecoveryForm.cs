using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Features.Authentication;

public sealed class RecoveryForm(HttpClient http)
{
    private string token = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public RecoveryAcknowledgement? Acknowledgement { get; private set; }
    public ApiError? Error { get; private set; }
    public bool Busy { get; private set; }
    public bool PasswordChanged { get; private set; }
    public bool HasResetLink => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(token);

    public string? FieldError(string name) =>
        Error?.Fields?.TryGetValue(name, out var message) == true ? message : null;

    public void LoadResetLink(Uri link)
    {
        var values = HttpUtility.ParseQueryString(link.Fragment.TrimStart('#'));
        Email = values["email"] ?? string.Empty;
        token = values["token"] ?? string.Empty;
    }

    public async Task RequestAsync()
    {
        if (Busy)
        {
            return;
        }

        Acknowledgement = null;
        Acknowledgement = await SendAsync<RecoveryAcknowledgement>("api/v1/auth/recoveries", new RequestRecoveryRequest(Email));
    }

    public async Task ResetAsync()
    {
        if (Busy || PasswordChanged)
        {
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            Error = new ApiError(ApiErrorCategory.Validation, "VALIDATION_FAILED", "Correct the invalid fields.",
                new Dictionary<string, string> { ["confirmPassword"] = "Passwords must match." });
            return;
        }

        var answer = await SendAsync<PasswordResetResponse>("api/v1/auth/password-resets",
            new ResetPasswordRequest(Email, token, NewPassword, ConfirmPassword));
        PasswordChanged = answer?.PasswordChanged == true;
        if (PasswordChanged)
        {
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            token = string.Empty;
        }
    }

    private async Task<T?> SendAsync<T>(string path, object input) where T : class
    {
        Busy = true;
        Error = null;
        try
        {
            using var response = await http.PostAsJsonAsync(path, input);
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
            if (answer?.Error is not null)
            {
                Error = answer.Error;
                return null;
            }

            if (!response.IsSuccessStatusCode || answer?.Data is null)
            {
                Error = Unavailable();
                return null;
            }

            return answer.Data;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            Error = Unavailable();
            return null;
        }
        finally
        {
            Busy = false;
        }
    }

    private static ApiError Unavailable() => new(ApiErrorCategory.Unavailable, "RECOVERY_UNAVAILABLE",
        "Recovery could not be confirmed. Try again.");
}
