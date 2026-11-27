using System.Net;
using System.Net.Http.Json;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;
using ReFrog.Contracts.IdentityVerification;

namespace ReFrog.Client.Tests.IdentityVerification;

/// <summary>A scripted server for the IDV endpoints. The antiforgery token is always served.</summary>
internal sealed class ScriptedApi : HttpMessageHandler
{
    public const string Eligibility = "GET /api/v1/me/shopping-eligibility";
    public const string Codes = "POST /api/v1/me/tcu-email/codes";
    public const string Verification = "POST /api/v1/me/tcu-email/verification";

    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> replies = [];

    public List<RequestTcuCodeRequest> CodeRequests { get; } = [];

    public List<VerifyTcuCodeRequest> CodeSubmissions { get; } = [];

    public int EligibilityReads { get; private set; }

    public HttpClient Client() => new(this) { BaseAddress = new Uri("https://localhost/") };

    public static ShoppingEligibilityResponse Status(bool eligible = false, string? basis = null, bool tcu = false,
        string access = VolunteerAccessValues.None, DateTimeOffset? pending = null) =>
        new(eligible, basis, tcu, access, pending);

    public ScriptedApi Reply<T>(string route, HttpStatusCode status, ApiResponse<T> body)
    {
        Queue(route).Enqueue(() => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });
        return this;
    }

    public ScriptedApi Ok(ShoppingEligibilityResponse status) =>
        Reply(Eligibility, HttpStatusCode.OK, ApiResponse.Success(status));

    public ScriptedApi Fail(string route, HttpStatusCode status, string code, IReadOnlyDictionary<string, string>? fields = null) =>
        Reply(route, status, ApiResponse.Failure<object>(new ApiError(
            (int)status >= 500 ? ApiErrorCategory.Unavailable : ApiErrorCategory.Validation, code, $"message for {code}", fields)));

    public ScriptedApi Raw(string route, HttpStatusCode status, string body = "")
    {
        Queue(route).Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body) });
        return this;
    }

    public ScriptedApi Drop(string route)
    {
        Queue(route).Enqueue(() => throw new HttpRequestException("Connection dropped."));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var route = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        if (route == "GET /api/v1/auth/antiforgery")
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(ApiResponse.Success(new AntiforgeryTokenResponse("token"))),
            };
        }

        switch (route)
        {
            case Eligibility:
                EligibilityReads++;
                break;
            case Codes:
                Assert.AreEqual("token", request.Headers.GetValues("RequestVerificationToken").Single());
                CodeRequests.Add((await request.Content!.ReadFromJsonAsync<RequestTcuCodeRequest>(cancellationToken))!);
                break;
            case Verification:
                Assert.AreEqual("token", request.Headers.GetValues("RequestVerificationToken").Single());
                CodeSubmissions.Add((await request.Content!.ReadFromJsonAsync<VerifyTcuCodeRequest>(cancellationToken))!);
                break;
        }

        Assert.IsTrue(replies.TryGetValue(route, out var queue) && queue.Count > 0, $"unscripted call {route}");
        return queue.Dequeue()();
    }

    private Queue<Func<HttpResponseMessage>> Queue(string route) =>
        replies.TryGetValue(route, out var queue) ? queue : replies[route] = new();
}
