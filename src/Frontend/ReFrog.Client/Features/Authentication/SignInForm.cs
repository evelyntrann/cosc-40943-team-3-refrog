using System.Net.Http.Json;
using System.Text.Json;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Features.Authentication;

public sealed class SignInForm(HttpClient http)
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public ApiError? Error { get; private set; }
    public bool Busy { get; private set; }

    public string? FieldError(string name) =>
        Error?.Fields?.TryGetValue(name, out var message) == true ? message : null;

    public async Task<SignInResponse?> SubmitAsync(string? returnPath)
    {
        Busy = true;
        Error = null;
        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/auth/session",
                new SignInRequest(Email, Password, returnPath));
            var answer = await response.Content.ReadFromJsonAsync<ApiResponse<SignInResponse>>();
            if (answer is null)
            {
                Error = Unavailable();
                return null;
            }

            Error = answer.Error;
            if (answer.Data is not null)
            {
                Password = string.Empty;
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

    private static ApiError Unavailable() => new(
        ApiErrorCategory.Unavailable,
        "AUTHENTICATION_UNAVAILABLE",
        "Sign-in is temporarily unavailable. Try again.");
}
