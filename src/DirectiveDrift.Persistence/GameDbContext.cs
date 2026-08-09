using Microsoft.EntityFrameworkCore;

namespace DirectiveDrift.Persistence;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<GuestProfileEntity> GuestProfiles => Set<GuestProfileEntity>();
    public DbSet<BuildEntity> Builds => Set<BuildEntity>();
    public DbSet<BuildVersionEntity> BuildVersions => Set<BuildVersionEntity>();
    public DbSet<RunEntity> Runs => Set<RunEntity>();
    public DbSet<RunSnapshotEntity> RunSnapshots => Set<RunSnapshotEntity>();
    public DbSet<TurnOperationEntity> TurnOperations => Set<TurnOperationEntity>();
    public DbSet<DecisionRecordEntity> DecisionRecords => Set<DecisionRecordEntity>();
    public DbSet<DomainEventEntity> DomainEvents => Set<DomainEventEntity>();
    public DbSet<CertificationEntity> Certifications => Set<CertificationEntity>();
    public DbSet<CertificationRunEntity> CertificationRuns => Set<CertificationRunEntity>();
    public DbSet<UsageLedgerEntity> UsageLedger => Set<UsageLedgerEntity>();
    public DbSet<SchemaMetadataEntity> SchemaMetadata => Set<SchemaMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuestProfileEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<BuildEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<BuildEntity>().HasIndex(entity => new { entity.OwnerId, entity.CreatedAt });
        modelBuilder.Entity<BuildVersionEntity>().HasKey(entity => new { entity.BuildId, entity.Version });
        modelBuilder.Entity<RunEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<RunEntity>().HasIndex(entity => new { entity.OwnerId, entity.CreatedAt });
        modelBuilder.Entity<RunSnapshotEntity>().HasKey(entity => new { entity.RunId, entity.Turn });
        modelBuilder.Entity<TurnOperationEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<TurnOperationEntity>()
            .HasIndex(entity => new { entity.RunId, entity.IdempotencyKey })
            .IsUnique();
        modelBuilder.Entity<TurnOperationEntity>()
            .HasIndex(entity => entity.RunId)
            .IsUnique()
            .HasFilter("Status IN (0, 1)");
        modelBuilder.Entity<DecisionRecordEntity>()
            .HasKey(entity => new { entity.RunId, entity.Turn, entity.AgentId });
        modelBuilder.Entity<DomainEventEntity>().HasKey(entity => new { entity.RunId, entity.Sequence });
        modelBuilder.Entity<CertificationEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<CertificationRunEntity>()
            .HasKey(entity => new { entity.CertificationId, entity.RunId });
        modelBuilder.Entity<UsageLedgerEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<UsageLedgerEntity>().HasIndex(entity => entity.OperationId).IsUnique();
        modelBuilder.Entity<SchemaMetadataEntity>().HasKey(entity => entity.Key);
        modelBuilder.Entity<SchemaMetadataEntity>().HasData(
            new SchemaMetadataEntity { Key = "schema-version", Value = "1" });

        modelBuilder.Entity<BuildEntity>()
            .HasOne<GuestProfileEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BuildVersionEntity>()
            .HasOne<BuildEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.BuildId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RunEntity>()
            .HasOne<GuestProfileEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RunEntity>()
            .HasOne<BuildVersionEntity>()
            .WithMany()
            .HasForeignKey(entity => new { entity.BuildId, entity.BuildVersion })
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RunSnapshotEntity>()
            .HasOne<RunEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TurnOperationEntity>()
            .HasOne<RunEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DecisionRecordEntity>()
            .HasOne<RunEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DomainEventEntity>()
            .HasOne<RunEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CertificationEntity>()
            .HasOne<GuestProfileEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CertificationRunEntity>()
            .HasOne<CertificationEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.CertificationId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CertificationRunEntity>()
            .HasOne<RunEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UsageLedgerEntity>()
            .HasOne<GuestProfileEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UsageLedgerEntity>()
            .HasOne<RunEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.RunId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UsageLedgerEntity>()
            .HasOne<TurnOperationEntity>()
            .WithOne()
            .HasForeignKey<UsageLedgerEntity>(entity => entity.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
