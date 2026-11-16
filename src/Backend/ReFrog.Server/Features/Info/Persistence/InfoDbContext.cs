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

/// <summary>Stored values of <see cref="DonationLocation.Status"/> (docs/design/INF.md data model; the list is <c>OI-32</c>).</summary>
public static class DonationLocationStatus
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string Full = "FULL";
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

/// <summary>A donation location of one event (docs/design/INF.md, data model).</summary>
public sealed class DonationLocation
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? MapPinUrl { get; set; }
    public string? InfographicUrl { get; set; }
    public string? PodNotes { get; set; }
    public required string Status { get; set; }
    public List<LocationSchedule> Schedules { get; } = [];
}

/// <summary>One weekday's operating hours at a location, in America/Chicago local time (<c>BR-event-schedule</c>).</summary>
public sealed class LocationSchedule
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }

    /// <summary><c>MONDAY</c> to <c>SATURDAY</c>, as INF.md's data model lists them.</summary>
    public required string DayOfWeek { get; set; }

    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
}

/// <summary>
/// The INF area's store. It keeps its tables and its migration history in the <c>info</c> schema,
/// so its migrations never mix with another area's in the one shared database (architecture §8.2.8).
/// </summary>
public sealed class InfoDbContext(DbContextOptions<InfoDbContext> options) : DbContext(options)
{
    public const string Schema = "info";

    public DbSet<ReFrogEvent> Events => Set<ReFrogEvent>();
    public DbSet<DonationLocation> Locations => Set<DonationLocation>();
    public DbSet<LocationSchedule> Schedules => Set<LocationSchedule>();

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
            // The current event is the one event whose status is PLANNED or ACTIVE (INF.md, EventConfigService).
            // Every current event computes the same key and every other event its own id, so the unique index
            // admits at most one current event. A unique index on Status alone would admit one of each.
            entity.Property<Guid>("CurrentEventKey")
                .HasComputedColumnSql(
                    "CASE WHEN [Status] IN ('PLANNED', 'ACTIVE') THEN CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier) ELSE [Id] END",
                    stored: true);
            entity.HasIndex("CurrentEventKey").IsUnique().HasDatabaseName("IX_ReFrogEvents_CurrentEvent");
        });
        modelBuilder.Entity<DonationLocation>(entity =>
        {
            entity.ToTable("DonationLocations", table =>
                table.HasCheckConstraint("CK_DonationLocation_Status", "[Status] IN ('OPEN', 'CLOSED', 'FULL')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.MapPinUrl).HasMaxLength(2048);
            entity.Property(x => x.InfographicUrl).HasMaxLength(2048);
            entity.Property(x => x.PodNotes).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.HasOne<ReFrogEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Schedules).WithOne().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LocationSchedule>(entity =>
        {
            entity.ToTable("LocationSchedules", table =>
            {
                table.HasCheckConstraint("CK_LocationSchedule_Day",
                    "[DayOfWeek] IN ('MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY')");
                table.HasCheckConstraint("CK_LocationSchedule_Hours", "[CloseTime] > [OpenTime]");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DayOfWeek).HasMaxLength(16);
        });
    }

    /// <summary>
    /// The SQL Server options for this context, shared by the app, tests, and EF tools so all three use the
    /// <c>info</c> migration history. <c>AuthDbContext</c> still configures itself inline; aligning it is a follow-up.
    /// </summary>
    public static void Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }
}
