using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Features.Volunteer;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Authentication;

// AUTH.md test rows "Volunteer state seam" and "Current user trust".
[TestClass]
public sealed class CurrentUserTests
{
    [TestMethod]
    public async Task RolesMapToVolunteerAccessWithRestrictionFirst()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var none = await fixture.CreateAccountAsync("none@example.org");
        var active = await fixture.CreateAccountAsync("active@example.org", VolunteerAuthorization.VolunteerRole);
        var restricted = await fixture.CreateAccountAsync("restricted@example.org",
            VolunteerAuthorization.VolunteerRole, VolunteerAuthorization.VolunteerRestrictedRole);
        var restrictedOnly = await fixture.CreateAccountAsync("restricted-only@example.org",
            VolunteerAuthorization.VolunteerRestrictedRole);

        Assert.AreEqual(VolunteerAccessState.None, await StateAsync(fixture, none));
        Assert.AreEqual(VolunteerAccessState.RoleActive, await StateAsync(fixture, active));
        Assert.AreEqual(VolunteerAccessState.Restricted, await StateAsync(fixture, restricted));
        Assert.AreEqual(VolunteerAccessState.Restricted, await StateAsync(fixture, restrictedOnly));
    }

    [TestMethod]
    public async Task RoleChangesAreSeenOnTheNextRead()
    {
        await using var fixture = await AffiliationFixture.CreateAsync();
        var account = await fixture.CreateAccountAsync("active@example.org", VolunteerAuthorization.VolunteerRole);
        Assert.AreEqual(VolunteerAccessState.RoleActive, await StateAsync(fixture, account));

        await fixture.WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<AuthAccount>>();
            var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            await roles.CreateAsync(new IdentityRole<Guid>(VolunteerAuthorization.VolunteerRestrictedRole));
            return await users.AddToRoleAsync((await users.FindByIdAsync(account.ToString()))!,
                VolunteerAuthorization.VolunteerRestrictedRole);
        });

        Assert.AreEqual(VolunteerAccessState.Restricted, await StateAsync(fixture, account));
    }

    [TestMethod]
    public async Task ReadFailureIsUnavailableNeverNone()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=Unreachable;Connect Timeout=1;TrustServerCertificate=True").Options;
        await using var db = new AuthDbContext(options);
        var provider = new EfVolunteerAccessStateProvider(db, new UpperInvariantLookupNormalizer());

        await Assert.ThrowsExactlyAsync<AccessStateUnavailableException>(
            () => provider.CurrentStateAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public void AccountIdComesOnlyFromTheAuthenticatedPrincipal()
    {
        var id = Guid.NewGuid();
        var signedIn = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "Cookie")),
        };
        signedIn.Request.Headers["X-Account-Id"] = Guid.NewGuid().ToString();
        signedIn.Request.QueryString = new QueryString($"?accountId={Guid.NewGuid()}");
        Assert.AreEqual(id, User(signedIn).AccountId);

        var anonymous = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())])),
        };
        Assert.IsNull(User(anonymous).AccountId, "an unauthenticated identity's claims are not trusted");
    }

    private static CurrentUser User(HttpContext context) =>
        new(new HttpContextAccessor { HttpContext = context }, new ThrowingProvider());

    private static Task<VolunteerAccessState> StateAsync(AffiliationFixture fixture, Guid account) =>
        fixture.WithServicesAsync(services => services.GetRequiredService<IVolunteerAccessStateProvider>()
            .CurrentStateAsync(account, CancellationToken.None));

    private sealed class ThrowingProvider : IVolunteerAccessStateProvider
    {
        public Task<VolunteerAccessState> CurrentStateAsync(Guid accountId, CancellationToken cancellationToken) =>
            throw new AccessStateUnavailableException();
    }
}
