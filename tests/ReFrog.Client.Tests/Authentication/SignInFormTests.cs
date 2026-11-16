using System.Net;
using System.Net.Http.Json;
using ReFrog.Client.Features.Authentication;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Tests.Authentication;

[TestClass]
public sealed class SignInFormTests
{
    [TestMethod]
    public async Task AcceptedCredentialsClearPasswordAndReturnServerDestination()
    {
        var server = new ScriptedServer(ApiResponse.Success(
            new SignInResponse(Guid.NewGuid(), "person@example.org", false, "/account")));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "Password1!";

        var session = await form.SubmitAsync("//outside.example");

        Assert.AreEqual("/account", session?.Destination);
        Assert.AreEqual(string.Empty, form.Password);
        Assert.AreEqual("//outside.example", server.Request?.ReturnPath);
    }

    [TestMethod]
    public async Task RejectedCredentialsKeepEmailAndPasswordForCorrection()
    {
        var server = new ScriptedServer(ApiResponse.Failure<SignInResponse>(new ApiError(
            ApiErrorCategory.Authorization, "INVALID_CREDENTIALS", "The email or password was not accepted.")));
        var form = server.Form();
        form.Email = "person@example.org";
        form.Password = "Wrong1!";

        var session = await form.SubmitAsync(null);

        Assert.IsNull(session);
        Assert.AreEqual("INVALID_CREDENTIALS", form.Error?.Code);
        Assert.AreEqual("person@example.org", form.Email);
        Assert.AreEqual("Wrong1!", form.Password);
    }

    [TestMethod]
    public async Task ValidationFailureNamesFieldAndUnavailableResponseIsSafe()
    {
        var validation = new ScriptedServer(ApiResponse.Failure<SignInResponse>(new ApiError(
            ApiErrorCategory.Validation, "VALIDATION_FAILED", "Enter an email address and password.",
            new Dictionary<string, string> { ["email"] = "Enter your email address." })));
        var validationForm = validation.Form();
        await validationForm.SubmitAsync(null);
        Assert.AreEqual("Enter your email address.", validationForm.FieldError("email"));

        var unavailable = new SignInForm(new HttpClient(new DroppedServer())
        {
            BaseAddress = new Uri("https://localhost/"),
        });
        await unavailable.SubmitAsync(null);
        Assert.AreEqual("AUTHENTICATION_UNAVAILABLE", unavailable.Error?.Code);
    }

    private sealed class ScriptedServer(ApiResponse<SignInResponse> reply) : HttpMessageHandler
    {
        public SignInRequest? Request { get; private set; }

        public SignInForm Form() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = await request.Content!.ReadFromJsonAsync<SignInRequest>(cancellationToken);
            return new HttpResponseMessage(reply.Error is null ? HttpStatusCode.OK : HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(reply),
            };
        }
    }

    private sealed class DroppedServer : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection dropped.");
    }
}
