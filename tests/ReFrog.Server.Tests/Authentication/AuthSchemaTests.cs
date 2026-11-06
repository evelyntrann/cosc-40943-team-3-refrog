using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ReFrog.Server.Features.Authentication.Persistence;

namespace ReFrog.Server.Tests.Authentication;

[TestClass]
public sealed class AuthSchemaTests
{
    [TestMethod]
    public void RegistrationRecordsHaveDatabaseUniquenessAndOwnershipConstraints()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=localhost;Database=ReFrogSchemaTest;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        using var context = new AuthDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;
        var pending = model.FindEntityType(typeof(PendingRegistration));
        var account = model.FindEntityType(typeof(AuthAccount));
        var challenge = model.FindEntityType(typeof(VerificationChallenge));

        Assert.IsNotNull(pending);
        Assert.IsNotNull(account);
        Assert.IsNotNull(challenge);
        Assert.IsTrue(pending.GetIndexes().Any(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(PendingRegistration.StartRequestId)])));
        Assert.IsTrue(account.GetIndexes().Any(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(AuthAccount.NormalizedEmail)])));
        Assert.IsTrue(challenge.GetCheckConstraints().Any(constraint => constraint.Name == "CK_VerificationChallenge_Owner"));
        Assert.IsTrue(challenge.GetForeignKeys().Any(key => key.Properties.Single().Name == nameof(VerificationChallenge.PendingRegistrationId)));
        Assert.IsTrue(challenge.GetForeignKeys().Any(key => key.Properties.Single().Name == nameof(VerificationChallenge.AccountId)));
    }
}
