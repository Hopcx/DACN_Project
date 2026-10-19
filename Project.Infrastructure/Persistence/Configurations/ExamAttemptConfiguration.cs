using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

public class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> entity)
    {
        entity.ToTable("ExamAttempts");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.State).HasMaxLength(20).IsRequired();
        entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.DraftJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.HasIndex(x => new { x.UserId, x.ExamScheduleId });
        entity.HasIndex(x => new { x.UserId, x.ExamScheduleId }).IsUnique()
            .HasFilter("[State] = 'inProgress'").HasDatabaseName("UX_ExamAttempts_Open");
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ExamSchedule>().WithMany().HasForeignKey(x => x.ExamScheduleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ExamDetail>().WithMany().HasForeignKey(x => x.ExamDetailId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExamAttemptSubmissionConfiguration : IEntityTypeConfiguration<ExamAttemptSubmission>
{
    public void Configure(EntityTypeBuilder<ExamAttemptSubmission> entity)
    {
        entity.ToTable("ExamAttemptSubmissions");
        entity.HasKey(x => x.ExamAttemptId);
        entity.Property(x => x.TotalMark).HasPrecision(18, 2);
        entity.Property(x => x.AnswersJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.VoidReason).HasMaxLength(1000);
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.VoidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Attempt).WithOne(x => x.Submission).HasForeignKey<ExamAttemptSubmission>(x => x.ExamAttemptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
