using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Features.Authentication;

/// <summary>
/// State and server calls behind <c>RegisterPage</c> (UC-AUTH-register-account).
/// A request ID is kept for one form fill and reused when no server answer arrived, so a
/// retry after a dropped connection replays the stored outcome instead of starting a second
/// registration or sending a second code (docs/design/AUTH.md, API contract).
/// </summary>
public sealed class RegistrationForm(HttpClient http)
{
    private Guid? startRequestId;
    private (string Email, string Password)? startInput;
    private Guid? resendRequestId;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public RegistrationChallengeResponse? Pending { get; private set; }

    public RegisteredAccountResponse? Completed { get; private set; }

    public ApiError? Error { get; private set; }

    public bool Busy { get; private set; }

    public string? FieldError(string name) =>
        Error?.Fields?.TryGetValue(name, out var message) == true ? message : null;

    public async Task StartAsync()
    {
        var input = (Email, Password);
        if (startRequestId is null || startInput != input)
        {
            startRequestId = Guid.NewGuid();
            startInput = input;
        }

        var answer = await PostAsync<RegistrationChallengeResponse>("api/v1/auth/registrations",
            new StartRegistrationRequest(Email, Password, startRequestId.Value),
            "Registration could not be confirmed. Try again.");
        if (answer is not null)
        {
            startRequestId = null;
            Pending = answer.Data;
            Error = answer.Error;
        }
    }

    public async Task ResendAsync()
    {
        if (Pending is null)
        {
            return;
        }

        resendRequestId ??= Guid.NewGuid();
        var answer = await PostAsync<RegistrationChallengeResponse>(
            $"api/v1/auth/registrations/{Pending.RegistrationId}/codes",
            new ResendRegistrationCodeRequest(resendRequestId.Value),
            "The code request could not be confirmed. Try again.");
        if (answer is not null)
        {
            resendRequestId = null;
            Pending = answer.Data ?? Pending;
            Error = answer.Error;
        }
    }

    public async Task VerifyAsync()
    {
        if (Pending is null)
        {
            return;
        }

        var answer = await PostAsync<RegisteredAccountResponse>(
            $"api/v1/auth/registrations/{Pending.RegistrationId}/verification",
            new VerifyRegistrationRequest(Code),
            "Verification could not be confirmed. Try again.");
        if (answer is not null)
        {
            Completed = answer.Data;
            Error = answer.Error;
            Code = string.Empty;
            if (Completed is not null)
            {
                Password = string.Empty;
            }
        }
    }

    // Returns null when no server answer was received, so the caller keeps its request ID.
    private async Task<ApiResponse<T>?> PostAsync<T>(string path, object body, string unavailableMessage)
    {
        Busy = true;
        Error = null;
        try
        {
            using var response = await http.PostAsJsonAsync(path, body);
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
            if (answer is null)
            {
                Error = Unavailable(unavailableMessage);
            }

            return answer;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException or TaskCanceledException)
        {
            Error = Unavailable(unavailableMessage);
            return null;
        }
        finally
        {
            Busy = false;
        }
    }

    private static ApiError Unavailable(string message) =>
        new(ApiErrorCategory.Unavailable, "NETWORK_UNAVAILABLE", message);
}
