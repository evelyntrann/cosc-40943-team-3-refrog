using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Administration.Persistence;

/// <summary>A volunteer shift (docs/design/ADM.md data model). Event and location ids refer to INF's records.</summary>
public sealed class Shift
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>UTC instant; entered in America/Chicago (ADM.md key decision 12).</summary>
    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }
    public int RequiredCount { get; set; }
    public Guid CreateRequestId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>SQL Server <c>rowversion</c>: changes on every write, so a stale edit can be refused (ADM.md data model).</summary>
    public byte[] Version { get; set; } = [];
}

/// <summary>A location's default required volunteer count for one event (<c>UC-ADM-manage-shifts</c> Associated Information).</summary>
public sealed class LocationStaffing
{
    public Guid EventId { get; set; }
    public Guid LocationId { get; set; }
    public int DefaultRequiredCount { get; set; }
}

/// <summary>An administrator change (architecture §8.2.9). No personal data beyond the actor's account id.</summary>
public sealed class AdminAuditEntry
{
    public Guid Id { get; set; }
    public Guid ActorAccountId { get; set; }
    public required string Action { get; set; }
    public required string TargetType { get; set; }
    public required string TargetId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>
/// ADM's store, in the <c>adm</c> schema with its own migration history, the same pattern as <c>InfoDbContext</c>.
/// No foreign key crosses into <c>info</c> (ADM.md key decision 11). In the app it runs on the scope's shared
/// connection (<see cref="ReFrog.Server.Persistence.SqlUnitOfWork"/>) so VOL can hold its shift lock.
/// </summary>
public sealed class AdmDbContext(DbContextOptions<AdmDbContext> options) : DbContext(options)
{
    public const string Schema = "adm";

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<LocationStaffing> LocationStaffing => Set<LocationStaffing>();
    public DbSet<AdminAuditEntry> AuditEntries => Set<AdminAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.ToTable("Shifts", table =>
            {
                table.HasCheckConstraint("CK_Shift_Times", "[EndsAt] > [StartsAt]");
                table.HasCheckConstraint("CK_Shift_RequiredCount", "[RequiredCount] > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasIndex(x => x.CreateRequestId).IsUnique();
            entity.HasIndex(x => new { x.EventId, x.StartsAt });
        });
        modelBuilder.Entity<LocationStaffing>(entity =>
        {
            entity.ToTable("LocationStaffing", table =>
                table.HasCheckConstraint("CK_LocationStaffing_DefaultRequiredCount", "[DefaultRequiredCount] > 0"));
            entity.HasKey(x => new { x.EventId, x.LocationId });
        });
        modelBuilder.Entity<AdminAuditEntry>(entity =>
        {
            entity.ToTable("AdminAuditEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(64);
            entity.Property(x => x.TargetType).HasMaxLength(64);
            entity.Property(x => x.TargetId).HasMaxLength(128);
            entity.HasIndex(x => new { x.ActorAccountId, x.OccurredAt });
        });
    }

    /// <summary>The app's options: the scope's shared connection and the <c>adm</c> migration history.</summary>
    public static void Configure(DbContextOptionsBuilder options, DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }

    /// <summary>The EF tools' options: a connection string and the <c>adm</c> migration history.</summary>
    public static void Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }
}
