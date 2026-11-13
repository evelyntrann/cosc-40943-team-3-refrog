using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReFrog.Server.Features.Info.Persistence;

public sealed class InfoDbContextFactory : IDesignTimeDbContextFactory<InfoDbContext>
{
    public InfoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ReFrog");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set ConnectionStrings__ReFrog before running EF Core tools.");
        }

        var options = new DbContextOptionsBuilder<InfoDbContext>();
        InfoDbContext.Configure(options, connectionString);
        return new InfoDbContext(options.Options);
    }
}
