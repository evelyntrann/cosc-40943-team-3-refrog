using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReFrog.Server.Features.Volunteer.Persistence;

public sealed class VolDbContextFactory : IDesignTimeDbContextFactory<VolDbContext>
{
    public VolDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ReFrog");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings__ReFrog before running EF Core tools.");
        }

        var options = new DbContextOptionsBuilder<VolDbContext>();
        VolDbContext.Configure(options, connectionString);
        return new VolDbContext(options.Options);
    }
}
