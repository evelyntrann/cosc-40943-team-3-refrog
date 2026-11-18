using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ReFrog.Server.Features.Authentication.Persistence;
using ReFrog.Server.Tests.Support;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class RecoverySchemaTests
{
    [TestMethod]
    public void RecoveryHistoryHasRequiredOwnershipAndDestinationTimeIndex()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var recovery = model.FindEntityType(typeof(RecoveryRequest));

        Assert.IsNotNull(recovery);
        Assert.IsTrue(recovery.GetProperties().Select(property => property.Name).Order()
            .SequenceEqual(["AcceptedAt", "AccountId", "Id", "NormalizedEmail"]));
        Assert.IsTrue(recovery.GetProperties().All(property => !property.IsNullable));
        Assert.AreEqual(256, recovery.FindProperty(nameof(RecoveryRequest.NormalizedEmail))?.GetMaxLength());
        Assert.AreEqual("datetimeoffset", recovery.FindProperty(nameof(RecoveryRequest.AcceptedAt))?.GetColumnType());
        var owner = recovery.GetForeignKeys().Single();
        Assert.AreEqual(typeof(AuthAccount), owner.PrincipalEntityType.ClrType);
        Assert.AreEqual(nameof(RecoveryRequest.AccountId), owner.Properties.Single().Name);
        Assert.AreEqual(DeleteBehavior.Restrict, owner.DeleteBehavior);
        Assert.IsTrue(recovery.GetIndexes().Any(index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(RecoveryRequest.NormalizedEmail), nameof(RecoveryRequest.AcceptedAt)])));
    }

    [TestMethod]
    public void RecoveryMigrationMatchesCurrentModelAndCreatesExpectedSql()
    {
        using var context = CreateContext();

        Assert.IsFalse(context.Database.HasPendingModelChanges());
        var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        var script = migrator.GenerateScript("20261113160000_AdministratorSignInAudit", "20261118120000_RecoveryRequests");
        StringAssert.Contains(script, "CREATE TABLE [RecoveryRequests]");
        StringAssert.Contains(script, "FOREIGN KEY ([AccountId]) REFERENCES [AspNetUsers] ([Id])");
        StringAssert.Contains(script, "CREATE INDEX [IX_RecoveryRequests_NormalizedEmail_AcceptedAt]");
        var rollback = migrator.GenerateScript("20261118120000_RecoveryRequests", "20261113160000_AdministratorSignInAudit");
        StringAssert.Contains(rollback, "DROP TABLE [RecoveryRequests]");
    }

    [TestMethod]
    public async Task MigratedSqlServerPersistsHistoryAndRejectsMissingAccount()
    {
        await using var host = await SqlTestHost.CreateAsync(DateTimeOffset.Parse(
            "2026-11-18T18:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = new AuthAccount
        {
            Id = Guid.NewGuid(),
            UserName = "recovery@example.org",
            Email = "recovery@example.org",
            EmailConfirmed = true,
        };
        var accounts = scope.ServiceProvider.GetRequiredService<UserManager<AuthAccount>>();
        Assert.IsTrue((await accounts.CreateAsync(account, "Password1!")).Succeeded);
        context.RecoveryRequests.Add(new RecoveryRequest
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            NormalizedEmail = "RECOVERY@EXAMPLE.ORG",
            AcceptedAt = host.Clock.GetUtcNow(),
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var saved = await context.RecoveryRequests.AsNoTracking().SingleAsync();
        Assert.AreEqual(account.Id, saved.AccountId);
        Assert.AreEqual("RECOVERY@EXAMPLE.ORG", saved.NormalizedEmail);
        Assert.AreEqual(host.Clock.GetUtcNow(), saved.AcceptedAt);

        context.RecoveryRequests.Add(new RecoveryRequest
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            NormalizedEmail = "UNKNOWN@EXAMPLE.ORG",
            AcceptedAt = host.Clock.GetUtcNow(),
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        Assert.AreEqual(1, await context.RecoveryRequests.CountAsync());
    }

    private static AuthDbContext CreateContext() => new(new DbContextOptionsBuilder<AuthDbContext>()
        .UseSqlServer("Server=localhost;Database=ReFrogSchemaTest;Integrated Security=True;TrustServerCertificate=True")
        .Options);
}
