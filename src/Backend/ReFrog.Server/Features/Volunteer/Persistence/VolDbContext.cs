using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Volunteer.Persistence;

/// <summary>
/// VOL's store, in the <c>vol</c> schema with its own migration history table, following the same isolated pattern
/// as <c>AdmDbContext</c> and <c>InfoDbContext</c> (architecture §8.2.8). In the app it runs on the scope's shared
/// connection (<see cref="ReFrog.Server.Persistence.SqlUnitOfWork"/>) so shift locking and assignment writes commit atomically.
/// </summary>
public sealed class VolDbContext(DbContextOptions<VolDbContext> options) : DbContext(options)
{
    public const string Schema = "vol";

    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ShiftAssignment>(entity =>
        {
            entity.ToTable("ShiftAssignments", table =>
            {
                table.HasCheckConstraint("CK_ShiftAssignment_Status",
                    "[Status] IN ('ACTIVE', 'PENDING', 'CANCELLED', 'DENIED')");
            });

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();

            // Two-layer duplicate prevention backstop (VOL.md key decisions)
            entity.HasIndex(x => new { x.ShiftId, x.VolunteerId })
                .IsUnique()
                .HasDatabaseName("UQ_ShiftAssignment_Shift_Volunteer");

            // Fast capacity query under lock
            entity.HasIndex(x => new { x.ShiftId, x.Status });

            // User schedule and role queries
            entity.HasIndex(x => new { x.VolunteerId, x.Status });
        });
    }

    /// <summary>The app's options: the scope's shared connection and the <c>vol</c> migration history.</summary>
    public static void Configure(DbContextOptionsBuilder options, DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }

    /// <summary>The EF tools' options: a connection string and the <c>vol</c> migration history.</summary>
    public static void Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }
}
