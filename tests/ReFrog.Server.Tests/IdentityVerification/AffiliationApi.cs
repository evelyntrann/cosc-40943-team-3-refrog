using System.Data.Common;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReFrog.Contracts.Api;
using ReFrog.Contracts.IdentityVerification;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.IdentityVerification;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.IdentityVerification;

/// <summary>Calls to the IDV.md endpoints, in the shape the Blazor client sends them.</summary>
internal static class AffiliationApi
{
    public static async Task<HttpResponseMessage> RequestCodeAsync(HttpClient client, string? email, Guid? requestId = null,
        bool withToken = true, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/tcu-email/codes")
        {
            Content = JsonContent.Create(new RequestTcuCodeRequest(email, requestId ?? Guid.NewGuid())),
        };
        if (withToken)
        {
            message.Headers.Add("RequestVerificationToken", await AffiliationFixture.TokenAsync(client));
        }

        return await client.SendAsync(message, cancellationToken);
    }

    public static async Task<HttpResponseMessage> VerifyAsync(HttpClient client, string? code, bool withToken = true)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/tcu-email/verification")
        {
            Content = JsonContent.Create(new VerifyTcuCodeRequest(code)),
        };
        if (withToken)
        {
            message.Headers.Add("RequestVerificationToken", await AffiliationFixture.TokenAsync(client));
        }

        return await client.SendAsync(message);
    }

    public static async Task<ShoppingEligibilityResponse> EligibilityAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/me/shopping-eligibility");
        Assert.AreEqual(200, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ShoppingEligibilityResponse>>())!.Data!;
    }

    public static async Task<ApiError?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<object>>())?.Error;

    public static async Task<string> VerifiedThroughApiAsync(HttpClient client, AffiliationFixture fixture, string email)
    {
        using var requested = await RequestCodeAsync(client, email);
        Assert.AreEqual(202, (int)requested.StatusCode);
        var code = fixture.Email.LastCode;
        using var verified = await VerifyAsync(client, code);
        Assert.AreEqual(200, (int)verified.StatusCode);
        return code;
    }

    public static string WrongCode(string code) => code == "000000" ? "000001" : "000000";

    public static Task SeedVerificationAsync(AffiliationFixture fixture, Guid account, string normalizedEmail) =>
        fixture.WithAuthAsync(db =>
        {
            db.TcuEmailVerifications.Add(new TcuEmailVerification
            {
                AccountId = account,
                NormalizedEmail = normalizedEmail,
                VerifiedAt = AffiliationFixture.Now,
            });
            return db.SaveChangesAsync();
        });

    /// <summary>Shopping's in-process check for one account, with the real provider and reader behind it.</summary>
    public static Task<ShoppingEligibility> ShoppingCheckAsync(AffiliationFixture fixture, Guid account,
        IVolunteerAccessStateProvider? access = null) =>
        fixture.WithServicesAsync(services => new AffiliationService(
                new FixedUser(account, access ?? services.GetRequiredService<IVolunteerAccessStateProvider>()),
                services.GetRequiredService<ITcuVerificationReader>(),
                services.GetRequiredService<VerificationChallengeService>())
            .CheckShoppingEligibilityAsync(CancellationToken.None));

    private sealed class FixedUser(Guid account, IVolunteerAccessStateProvider access) : ICurrentUser
    {
        public Guid? AccountId => account;

        public Task<VolunteerAccessState> CurrentVolunteerAccessAsync(CancellationToken cancellationToken) =>
            access.CurrentStateAsync(account, cancellationToken);
    }
}

/// <summary>Switchable failures for 2d and the save-failure rows (§8.2.10: persistence failures).</summary>
internal sealed class Faults
{
    public bool AccessFails { get; set; }
    public bool ReaderFails { get; set; }
    public bool ChallengeReadsFail { get; set; }
    public bool CommitFails { get; set; }
    public bool SendOutcomeWriteFails { get; set; }

    public void Install(IServiceCollection services)
    {
        services.AddScoped<EfVolunteerAccessStateProvider>();
        services.RemoveAll<IVolunteerAccessStateProvider>();
        services.AddScoped<IVolunteerAccessStateProvider>(provider =>
            new SwitchableAccess(provider.GetRequiredService<EfVolunteerAccessStateProvider>(), this));
        services.AddScoped<EfTcuVerificationReader>();
        services.RemoveAll<ITcuVerificationReader>();
        services.AddScoped<ITcuVerificationReader>(provider =>
            new SwitchableReader(provider.GetRequiredService<EfTcuVerificationReader>(), this));
        services.ConfigureDbContext<AuthDbContext>(options =>
            options.AddInterceptors(new CommandFaults(this), new CommitFaults(this)));
    }

    public sealed class AlwaysUnavailable : IVolunteerAccessStateProvider
    {
        public Task<VolunteerAccessState> CurrentStateAsync(Guid accountId, CancellationToken cancellationToken) =>
            throw new AccessStateUnavailableException();
    }

    private sealed class SwitchableAccess(IVolunteerAccessStateProvider inner, Faults faults) : IVolunteerAccessStateProvider
    {
        public Task<VolunteerAccessState> CurrentStateAsync(Guid accountId, CancellationToken cancellationToken) =>
            faults.AccessFails ? throw new AccessStateUnavailableException() : inner.CurrentStateAsync(accountId, cancellationToken);
    }

    private sealed class SwitchableReader(ITcuVerificationReader inner, Faults faults) : ITcuVerificationReader
    {
        public Task<TcuVerificationState> ForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
            faults.ReaderFails ? throw new AccessStateUnavailableException() : inner.ForAccountAsync(accountId, cancellationToken);
    }

    private sealed class CommandFaults(Faults faults) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Check(command);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Check(DbCommand command)
        {
            var text = command.CommandText;
            var verificationTable = text.Contains("[VerificationChallenges]", StringComparison.Ordinal) ||
                text.Contains("[TcuEmailVerifications]", StringComparison.Ordinal);
            if (faults.ChallengeReadsFail && verificationTable && text.TrimStart().StartsWith("SELECT", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("injected read failure");
            }

            if (faults.SendOutcomeWriteFails && text.Contains("UPDATE", StringComparison.Ordinal) &&
                text.Contains("[SendOutcome]", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("injected outcome write failure");
            }
        }
    }

    private sealed class CommitFaults(Faults faults) : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            faults.CommitFails
                ? throw new InvalidOperationException("injected commit failure")
                : base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }
}
