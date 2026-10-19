using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

public sealed class LegacyImportBatchConfiguration : IEntityTypeConfiguration<LegacyImportBatch>
{
    public void Configure(EntityTypeBuilder<LegacyImportBatch> e)
    {
        e.ToTable("LegacyImportBatches");
        e.HasKey(x => x.Id);
        e.Property(x => x.SourceSystem).HasMaxLength(80).IsRequired();
        e.Property(x => x.SourceDatabase).HasMaxLength(128).IsRequired();
        e.Property(x => x.SourceMigration).HasMaxLength(150).IsRequired();
        e.Property(x => x.Marker).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.SourceSystem, x.SourceDatabase, x.Marker }).IsUnique();
    }
}

public sealed class LegacyUserMapConfiguration : IEntityTypeConfiguration<LegacyUserMap>
{
    public void Configure(EntityTypeBuilder<LegacyUserMap> e)
    {
        e.ToTable("LegacyUserMaps");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.BatchId, x.SourceUserId }).IsUnique();
        e.HasOne<LegacyImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LegacySubmissionRecordConfiguration : IEntityTypeConfiguration<LegacySubmissionRecord>
{
    public void Configure(EntityTypeBuilder<LegacySubmissionRecord> e)
    {
        e.ToTable("LegacySubmissionRecords");
        e.HasKey(x => x.Id);
        e.Property(x => x.SourceTable).HasMaxLength(80).IsRequired();
        e.Property(x => x.ExamName).HasMaxLength(500).IsRequired();
        e.Property(x => x.ScheduleTitle).HasMaxLength(500);
        e.Property(x => x.SubmitTimeUnknown).HasColumnType("datetime2");
        e.Property(x => x.TimeTaken).HasColumnType("time");
        e.Property(x => x.TotalMark).HasColumnType("float");
        e.Property(x => x.SourceChecksum).HasColumnType("binary(32)");
        e.HasIndex(x => new { x.BatchId, x.SourceTable, x.SourceId }).IsUnique();
        e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LegacyAnswerSelectionConfiguration : IEntityTypeConfiguration<LegacyAnswerSelection>
{
    public void Configure(EntityTypeBuilder<LegacyAnswerSelection> e)
    {
        e.ToTable("LegacyAnswerSelections");
        e.HasKey(x => x.Id);
        e.Property(x => x.SourceTable).HasMaxLength(80).IsRequired();
        e.Property(x => x.SourceChecksum).HasColumnType("binary(32)");
        e.HasIndex(x => new { x.LegacySubmissionRecordId, x.SourceTable, x.SourceId }).IsUnique();
        e.HasOne<LegacySubmissionRecord>().WithMany().HasForeignKey(x => x.LegacySubmissionRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
