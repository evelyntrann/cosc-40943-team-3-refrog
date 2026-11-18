using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReFrog.Client.Features.Authentication;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.Authentication;

namespace ReFrog.Client.Tests.Authentication;

[TestClass]
public sealed class RecoveryFormTests
{
    private const string ResetUri = "https://localhost/reset-password#email=Person%40example.org&token=protected%2Btoken%2F%3Dvalue";

    [TestMethod]
    public async Task AcknowledgementIsDisplayedWithAnImmediateRetryAndSignInLink()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(new RecoveryAcknowledgement("If an account matches, a link will be sent.")));
        var form = server.Form();
        form.Email = "Person@example.org";
        await form.RequestAsync();

        Assert.AreEqual("Person@example.org", form.Email);
        Assert.IsFalse(form.Busy);
        var html = await RenderAsync<RecoverAccountPage>(form);
        StringAssert.Contains(html, "If an account matches, a link will be sent.");
        StringAssert.Contains(html, "Request reset link");
        StringAssert.Contains(html, "href=\"sign-in\"");
        StringAssert.Contains(html, "type=\"email\"");
    }

    [TestMethod]
    public async Task RecoveryValidationKeepsEmailAndRendersTheNamedField()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.UnprocessableEntity, ApiResponse.Failure<RecoveryAcknowledgement>(new ApiError(
            ApiErrorCategory.Validation, "VALIDATION_FAILED", "Correct the email.",
            new Dictionary<string, string> { ["email"] = "Enter a valid email address." })));
        var form = server.Form();
        form.Email = "typed-address";
        await form.RequestAsync();

        var html = await RenderAsync<RecoverAccountPage>(form);
        Assert.AreEqual("typed-address", form.Email);
        StringAssert.Contains(html, "Enter a valid email address.");
        StringAssert.Contains(html, "recovery-email-error");
    }

    [TestMethod]
    public async Task ResetValidationPreservesInputAndRendersServerPasswordFeedback()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.UnprocessableEntity, ApiResponse.Failure<PasswordResetResponse>(new ApiError(
            ApiErrorCategory.Validation, "VALIDATION_FAILED", "Correct the invalid fields.",
            new Dictionary<string, string> { ["newPassword"] = "Password policy feedback from the server." })));
        var form = server.Form();
        form.LoadResetLink(new Uri(ResetUri));
        form.NewPassword = "typed-password";
        await form.ResetAsync();

        Assert.AreEqual("Person@example.org", form.Email);
        Assert.AreEqual("typed-password", form.NewPassword);
        Assert.IsTrue(form.HasResetLink);
        Assert.AreEqual("protected+token/=value", server.LastReset?.Token);
        var navigation = new TestNavigation(ResetUri);
        var html = await RenderAsync<ResetPasswordPage>(form, navigation);
        StringAssert.Contains(html, "Password policy feedback from the server.");
        StringAssert.Contains(html, "autocomplete=\"new-password\"");
        Assert.AreEqual("https://localhost/reset-password", navigation.Uri);
        Assert.IsTrue(navigation.ReplacedHistory);
        Assert.DoesNotContain("protected+token", html, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task SuccessfulResetClearsCredentialsAndRendersReturnToSignIn()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(new PasswordResetResponse(true)));
        var form = server.Form();
        form.LoadResetLink(new Uri(ResetUri));
        form.NewPassword = "NewPassword2!";
        await form.ResetAsync();

        Assert.IsTrue(form.PasswordChanged);
        Assert.AreEqual(string.Empty, form.NewPassword);
        Assert.IsFalse(form.HasResetLink);
        var html = await RenderAsync<ResetPasswordPage>(form);
        StringAssert.Contains(html, "Your password has been changed.");
        StringAssert.Contains(html, "Sign in with your new password");
        Assert.DoesNotContain("type=\"password\"", html, StringComparison.Ordinal);
        await form.ResetAsync();
        Assert.AreEqual(1, server.RequestCount);
    }

    [TestMethod]
    public async Task InvalidOrMissingTokenRendersRequestNewLink()
    {
        var server = new ScriptedServer();
        var form = server.Form();
        var missing = await RenderAsync<ResetPasswordPage>(form);
        StringAssert.Contains(missing, "Request a new recovery link");
        Assert.DoesNotContain("type=\"password\"", missing, StringComparison.Ordinal);

        server.Reply(HttpStatusCode.UnprocessableEntity, ApiResponse.Failure<PasswordResetResponse>(new ApiError(
            ApiErrorCategory.Validation, "RESET_TOKEN_INVALID", "Request a new recovery link.")));
        form.LoadResetLink(new Uri(ResetUri));
        form.NewPassword = "NewPassword2!";
        await form.ResetAsync();
        var invalid = await RenderAsync<ResetPasswordPage>(form, new TestNavigation(ResetUri));
        StringAssert.Contains(invalid, "href=\"recover-account\"");
        Assert.DoesNotContain("type=\"password\"", invalid, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task FailedSaveKeepsResetInputAndAllowsRetryWithTheSameToken()
    {
        var server = new ScriptedServer();
        server.Reply(HttpStatusCode.ServiceUnavailable, ApiResponse.Failure<PasswordResetResponse>(new ApiError(
            ApiErrorCategory.Unavailable, "NOT_RECORDED", "The password change was not recorded. Try again.")));
        server.Reply(HttpStatusCode.OK, ApiResponse.Success(new PasswordResetResponse(true)));
        var form = server.Form();
        form.LoadResetLink(new Uri(ResetUri));
        form.NewPassword = "NewPassword2!";
        await form.ResetAsync();
        var html = await RenderAsync<ResetPasswordPage>(form, new TestNavigation(ResetUri));
        StringAssert.Contains(html, "The password change was not recorded. Try again.");
        Assert.AreEqual("NewPassword2!", form.NewPassword);
        Assert.IsTrue(form.HasResetLink);
        await form.ResetAsync();
        Assert.IsTrue(form.PasswordChanged);
        Assert.AreEqual(2, server.RequestCount);
        Assert.AreEqual("protected+token/=value", server.LastReset?.Token);
    }

    [TestMethod]
    public async Task MalformedOrDroppedResponseKeepsInputAndDoesNotClaimSuccess()
    {
        var server = new ScriptedServer();
        server.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json") });
        var form = server.Form();
        form.Email = "Person@example.org";
        await form.RequestAsync();
        Assert.IsNull(form.Acknowledgement);
        Assert.AreEqual("RECOVERY_UNAVAILABLE", form.Error?.Code);
        Assert.AreEqual("Person@example.org", form.Email);

        server.DropConnection = true;
        form.LoadResetLink(new Uri(ResetUri));
        form.NewPassword = "NewPassword2!";
        await form.ResetAsync();
        Assert.IsFalse(form.PasswordChanged);
        Assert.AreEqual("NewPassword2!", form.NewPassword);
        Assert.IsTrue(form.HasResetLink);
        Assert.IsFalse(form.Busy);
    }

    [TestMethod]
    public async Task BusyFormPreventsDuplicateRequests()
    {
        var server = new ScriptedServer { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        server.Reply(HttpStatusCode.Accepted, ApiResponse.Success(new RecoveryAcknowledgement("Check your email.")));
        var form = server.Form();
        form.Email = "Person@example.org";
        var first = form.RequestAsync();
        Assert.IsTrue(form.Busy);
        await form.RequestAsync();
        Assert.AreEqual(1, server.RequestCount);
        server.Gate.SetResult();
        await first;
        Assert.IsFalse(form.Busy);
    }

    private static async Task<string> RenderAsync<T>(RecoveryForm form, TestNavigation? navigation = null)
        where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(form);
        services.AddSingleton<NavigationManager>(navigation ?? new TestNavigation("https://localhost/reset-password"));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<T>();
            return component.ToHtmlString();
        });
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string uri) => Initialize("https://localhost/", uri);
        public bool ReplacedHistory { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            ReplacedHistory = options.ReplaceHistoryEntry;
        }
    }

    private sealed class ScriptedServer : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public ResetPasswordRequest? LastReset { get; private set; }
        public int RequestCount { get; private set; }
        public bool DropConnection { get; set; }
        public TaskCompletionSource? Gate { get; init; }

        public RecoveryForm Form() => new(new HttpClient(this) { BaseAddress = new Uri("https://localhost/") });

        public void Reply<T>(HttpStatusCode status, ApiResponse<T> response) => Responses.Enqueue(
            new HttpResponseMessage(status) { Content = JsonContent.Create(response) });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (DropConnection)
            {
                throw new HttpRequestException("Dropped test connection.");
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("password-resets", StringComparison.Ordinal) == true)
            {
                LastReset = await request.Content!.ReadFromJsonAsync<ResetPasswordRequest>(cancellationToken);
            }

            if (Gate is not null)
            {
                await Gate.Task;
            }

            return Responses.Dequeue();
        }
    }
}
