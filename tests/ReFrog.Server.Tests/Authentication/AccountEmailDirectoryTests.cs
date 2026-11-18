using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Authentication;
using ReFrog.Server.Tests.Administration;

namespace ReFrog.Server.Tests.Authentication;

// AUTH's read contract for the UC-ADM-manage-shifts 6a1 preview (AUTH.md Components). Sample data only.
[TestClass]
public sealed class AccountEmailDirectoryTests
{
    [TestMethod]
    public async Task ReturnsTheEmailsOfKnownAccountsAndLeavesOutUnknownIds()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(withEvent: false);
        var first = await fixture.CreateAccountAsync("first.volunteer@example.test", isAdministrator: false);
        var second = await fixture.CreateAccountAsync("second.volunteer@example.test", isAdministrator: false);
        await fixture.CreateAccountAsync("not.asked.for@example.test", isAdministrator: false);
        var unknown = Guid.NewGuid();

        await using var scope = fixture.Host.Factory.Services.CreateAsyncScope();
        var emails = await scope.ServiceProvider.GetRequiredService<IAccountEmailDirectory>()
            .GetEmailsAsync([first, second, first, unknown], CancellationToken.None);

        Assert.HasCount(2, emails);
        Assert.AreEqual("first.volunteer@example.test", emails[first]);
        Assert.AreEqual("second.volunteer@example.test", emails[second]);
    }

    [TestMethod]
    public async Task NoIdsMeansNoEmails()
    {
        await using var fixture = await AdminShiftFixture.CreateAsync(withEvent: false);

        await using var scope = fixture.Host.Factory.Services.CreateAsyncScope();
        var emails = await scope.ServiceProvider.GetRequiredService<IAccountEmailDirectory>()
            .GetEmailsAsync([], CancellationToken.None);

        Assert.IsEmpty(emails);
    }
}
