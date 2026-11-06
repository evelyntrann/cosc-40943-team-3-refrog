using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReFrog.Server.Features.Authentication.Persistence;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=localhost;Database=ReFrogDesign;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        return new AuthDbContext(options);
    }
}
