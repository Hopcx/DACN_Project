using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

// To-be T5: phiên luyện tập. Chỉ thêm bảng, không đụng ExamAttempts.
public class PracticeSessionConfiguration : IEntityTypeConfiguration<PracticeSession>
{
    public void Configure(EntityTypeBuilder<PracticeSession> entity)
    {
        entity.ToTable("PracticeSessions", t =>
        {
            t.HasCheckConstraint("CK_PracticeSessions_Kind", "[Kind] IN ('practice','mock')");
            t.HasCheckConstraint("CK_PracticeSessions_RevealMode", "[RevealMode] IN ('end','perQuestion')");
            t.HasCheckConstraint("CK_PracticeSessions_State", "[State] IN ('inProgress','finished')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        entity.Property(x => x.RevealMode).HasMaxLength(20).IsRequired();
        entity.Property(x => x.State).HasMaxLength(20).IsRequired();
        entity.Property(x => x.CriteriaJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.DraftJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
