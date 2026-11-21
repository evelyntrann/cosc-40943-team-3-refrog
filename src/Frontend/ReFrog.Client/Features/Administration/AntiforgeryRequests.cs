using System.Net.Http.Json;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Features.Administration;

/// <summary>The antiforgery token every administrator write must carry (ADM.md key decision 7).</summary>
internal static class AntiforgeryRequests
{
    private const string TokenHeader = "RequestVerificationToken";

    /// <summary>The current token, or null when the server gave none.</summary>
    public static async Task<string?> FetchTokenAsync(HttpClient http)
    {
        var answer = await http.GetFromJsonAsync<ApiResponse<AntiforgeryTokenResponse>>(
            new Uri("api/v1/auth/antiforgery", UriKind.Relative));
        return answer?.Data?.Token;
    }

    public static HttpRequestMessage Create(HttpMethod method, string path, string token)
    {
        var message = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        message.Headers.Add(TokenHeader, token);
        return message;
    }

    public static HttpRequestMessage Create<T>(HttpMethod method, string path, string token, T body)
    {
        var message = Create(method, path, token);
        message.Content = JsonContent.Create(body);
        return message;
    }
}
