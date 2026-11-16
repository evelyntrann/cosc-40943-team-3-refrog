using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReFrog.Server.Features.Administration.Persistence;

public sealed class AdmDbContextFactory : IDesignTimeDbContextFactory<AdmDbContext>
{
    public AdmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ReFrog");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings__ReFrog before running EF Core tools.");
        }

        var options = new DbContextOptionsBuilder<AdmDbContext>();
        AdmDbContext.Configure(options, connectionString);
        return new AdmDbContext(options.Options);
    }
}
