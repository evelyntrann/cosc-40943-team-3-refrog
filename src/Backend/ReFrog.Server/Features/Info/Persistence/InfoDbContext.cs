using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Info.Persistence;

/// <summary>Stored values of <see cref="ReFrogEvent.Status"/> (SRS §7.2, <c>ReFrogEvent.status</c>).</summary>
public static class ReFrogEventStatus
{
    public const string Planned = "PLANNED";
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
    public const string Archived = "ARCHIVED";
}

/// <summary>One move-out event season (docs/design/INF.md, data model).</summary>
public sealed class ReFrogEvent
{
    public Guid Id { get; set; }
    public int EventYear { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public required string DailyOperatingHoursDescription { get; set; }
    public required string CampusOverview { get; set; }
    public string? OverallInfographicUrl { get; set; }
    public required string Status { get; set; }
}

/// <summary>
/// The INF area's store. It keeps its tables and its migration history in the <c>info</c> schema,
/// so its migrations never mix with another area's in the one shared database (architecture §8.2.8).
/// </summary>
public sealed class InfoDbContext(DbContextOptions<InfoDbContext> options) : DbContext(options)
{
    public const string Schema = "info";

    public DbSet<ReFrogEvent> Events => Set<ReFrogEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ReFrogEvent>(entity =>
        {
            entity.ToTable("ReFrogEvents", table =>
            {
                table.HasCheckConstraint("CK_ReFrogEvent_Status", "[Status] IN ('PLANNED', 'ACTIVE', 'CLOSED', 'ARCHIVED')");
                table.HasCheckConstraint("CK_ReFrogEvent_Dates", "[EndDate] >= [StartDate]");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.DailyOperatingHoursDescription).HasMaxLength(500);
            entity.Property(x => x.CampusOverview).HasMaxLength(4000);
            entity.Property(x => x.OverallInfographicUrl).HasMaxLength(2048);
            // UC-INF-view-event-info reads "the" active event, so at most one may be ACTIVE.
            entity.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] = 'ACTIVE'");
        });
    }

    /// <summary>The SQL Server options every caller (app, tests, EF tools) must use for this context.</summary>
    public static void Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }
}
