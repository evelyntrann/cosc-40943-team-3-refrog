using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ReFrog.Server.Features.Authentication.Persistence;

public sealed class AuthAccount : IdentityUser<Guid>;

public sealed class PendingRegistration
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public Guid StartRequestId { get; set; }
    public required string State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? AccountId { get; set; }
    public AuthAccount? Account { get; set; }
    public List<VerificationChallenge> Challenges { get; set; } = [];
}

public sealed class VerificationChallenge
{
    public Guid Id { get; set; }
    public required string Purpose { get; set; }
    public Guid? PendingRegistrationId { get; set; }
    public PendingRegistration? PendingRegistration { get; set; }
    public Guid? AccountId { get; set; }
    public AuthAccount? Account { get; set; }
    public required string NormalizedEmail { get; set; }
    public required byte[] CodeHmac { get; set; }
    public int KeyVersion { get; set; }
    public Guid RequestId { get; set; }
    public string? SendOutcome { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public List<VerificationFailure> Failures { get; set; } = [];
}

public sealed class VerificationFailure
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public VerificationChallenge Challenge { get; set; } = null!;
    public DateTimeOffset FailedAt { get; set; }
}

public sealed class TcuEmailVerification
{
    public Guid AccountId { get; set; }
    public AuthAccount Account { get; set; } = null!;
    public required string NormalizedEmail { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}

public sealed class AdministratorSignInAudit
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public bool Succeeded { get; set; }
}

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<AuthAccount, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
    public DbSet<VerificationChallenge> VerificationChallenges => Set<VerificationChallenge>();
    public DbSet<VerificationFailure> VerificationFailures => Set<VerificationFailure>();
    public DbSet<TcuEmailVerification> TcuEmailVerifications => Set<TcuEmailVerification>();
    public DbSet<AdministratorSignInAudit> AdministratorSignInAudits => Set<AdministratorSignInAudit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AuthAccount>().HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Entity<PendingRegistration>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.StartRequestId).IsUnique();
            entity.HasIndex(x => x.AccountId).IsUnique().HasFilter("[AccountId] IS NOT NULL");
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(256);
            entity.Property(x => x.State).HasMaxLength(32);
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VerificationChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasMaxLength(32);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(256);
            entity.Property(x => x.CodeHmac).HasMaxLength(32);
            entity.Property(x => x.SendOutcome).HasMaxLength(16);
            entity.HasOne(x => x.PendingRegistration).WithMany(x => x.Challenges).HasForeignKey(x => x.PendingRegistrationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.Purpose, x.PendingRegistrationId, x.RequestId }).IsUnique()
                .HasFilter("[PendingRegistrationId] IS NOT NULL");
            entity.HasIndex(x => new { x.Purpose, x.AccountId, x.RequestId }).IsUnique()
                .HasFilter("[AccountId] IS NOT NULL");
            entity.HasIndex(x => new { x.Purpose, x.PendingRegistrationId }).IsUnique()
                .HasFilter("[PendingRegistrationId] IS NOT NULL AND [ConsumedAt] IS NULL AND [SupersededAt] IS NULL");
            entity.HasIndex(x => new { x.Purpose, x.AccountId }).IsUnique()
                .HasFilter("[AccountId] IS NOT NULL AND [ConsumedAt] IS NULL AND [SupersededAt] IS NULL");
            entity.ToTable(table => table.HasCheckConstraint("CK_VerificationChallenge_Owner",
                "([Purpose] = 'ACCOUNT_REGISTRATION' AND [PendingRegistrationId] IS NOT NULL AND [AccountId] IS NULL) OR ([Purpose] = 'TCU_AFFILIATION' AND [AccountId] IS NOT NULL AND [PendingRegistrationId] IS NULL)"));
        });

        builder.Entity<VerificationFailure>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Challenge).WithMany(x => x.Failures).HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.FailedAt);
        });

        builder.Entity<TcuEmailVerification>(entity =>
        {
            entity.HasKey(x => x.AccountId);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(256);
            entity.HasOne(x => x.Account).WithOne().HasForeignKey<TcuEmailVerification>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AdministratorSignInAudit>(entity =>
        {
            entity.ToTable("AdministratorSignInAudits");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.AccountId, item.OccurredAt });
            entity.Property(item => item.OccurredAt).HasColumnType("datetimeoffset");
        });
    }
}
