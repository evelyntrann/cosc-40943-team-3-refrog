using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Authentication;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Tests.Authentication;

// UC-AUTH-register-account client behavior behind RegisterPage (AUTH.md test row "Registration 3b validation",
// and request-ID reuse from the API contract).
[TestClass]
public sealed class RegistrationFormTests
{
    private static readonly RegistrationChallengeResponse Challenge =
        new(Guid.NewGuid(), new DateTimeOffset(2026, 11, 6, 18, 10, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 6, 18, 1, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task ValidationFailureNamesTheFieldAndKeepsTheInput()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.UnprocessableEntity, ApiResponse.Failure<RegistrationChallengeResponse>(
            new ApiError(ApiErrorCategory.Validation, "VALIDATION_FAILED", "Correct the invalid fields.",
                new Dictionary<string, string> { ["password"] = "Use at least six characters." })));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "short";

        await form.StartAsync();

        Assert.AreEqual("Use at least six characters.", form.FieldError("password"));
        Assert.IsNull(form.FieldError("email"));
        Assert.AreEqual("person@example.org", form.Email);
        Assert.AreEqual("short", form.Password);
        Assert.IsNull(form.Pending);
    }

    [TestMethod]
    public async Task RetryAfterLostConnectionReusesTheStartRequestId()
    {
        var server = new ScriptedServer();
        server.Drop();
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "Password1!";

        await form.StartAsync();
        Assert.AreEqual("NETWORK_UNAVAILABLE", form.Error?.Code);
        await form.StartAsync();

        Assert.AreEqual(server.StartRequestIds[0], server.StartRequestIds[1]);
        Assert.AreEqual(Challenge, form.Pending);
        Assert.IsNull(form.Error);
    }

    [TestMethod]
    public async Task ChangedInputGetsANewStartRequestId()
    {
        var server = new ScriptedServer();
        server.Drop();
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "Password1!";

        await form.StartAsync();
        form.Email = "other@example.org";
        await form.StartAsync();

        Assert.AreNotEqual(server.StartRequestIds[0], server.StartRequestIds[1]);
    }

    [TestMethod]
    public async Task AnsweredStartGetsANewRequestIdNextTime()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.ServiceUnavailable, ApiResponse.Failure<RegistrationChallengeResponse>(
            new ApiError(ApiErrorCategory.Unavailable, "EMAIL_NOT_SENT", "Not sent.")));
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "Password1!";

        await form.StartAsync();
        Assert.AreEqual("EMAIL_NOT_SENT", form.Error?.Code);
        await form.StartAsync();

        Assert.AreNotEqual(server.StartRequestIds[0], server.StartRequestIds[1]);
        Assert.AreEqual(Challenge, form.Pending);
    }

    [TestMethod]
    public async Task ResendReusesItsRequestIdOnlyUntilAnswered()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        server.Drop();
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "Password1!";
        await form.StartAsync();

        await form.ResendAsync();
        await form.ResendAsync();
        await form.ResendAsync();

        Assert.AreEqual(server.ResendRequestIds[0], server.ResendRequestIds[1]);
        Assert.AreNotEqual(server.ResendRequestIds[1], server.ResendRequestIds[2]);
    }

    [TestMethod]
    public async Task SuccessfulVerificationClearsPasswordAndCode()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(Challenge));
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(
            new RegisteredAccountResponse(Guid.NewGuid(), "student@tcu.edu", true)));
        var form = server.Form();
        form.Email = "student@tcu.edu";
        form.Password = "Password1!";
        await form.StartAsync();
        form.Code = "123456";

        await form.VerifyAsync();

        Assert.IsTrue(form.Completed?.TcuEmailVerified);
        Assert.AreEqual(string.Empty, form.Password);
        Assert.AreEqual(string.Empty, form.Code);
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> replies = new();

        public List<Guid> StartRequestIds { get; } = [];

        public List<Guid> ResendRequestIds { get; } = [];

        public RegistrationForm Form() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        public void Reply<T>(HttpStatusCode status, ApiResponse<T> body) =>
            replies.Enqueue(() => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

        public void Drop() => replies.Enqueue(() => throw new HttpRequestException("Connection dropped."));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/auth/registrations")
            {
                var body = await request.Content!.ReadFromJsonAsync<StartRegistrationRequest>(cancellationToken);
                StartRequestIds.Add(body!.RequestId);
            }
            else if (path.EndsWith("/codes", StringComparison.Ordinal))
            {
                var body = await request.Content!.ReadFromJsonAsync<ResendRegistrationCodeRequest>(cancellationToken);
                ResendRequestIds.Add(body!.RequestId);
            }

            return replies.Dequeue()();
        }
    }
}
