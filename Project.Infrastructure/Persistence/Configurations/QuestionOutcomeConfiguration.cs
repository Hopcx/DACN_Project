using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

// To-be T11: kết quả từng câu. Chỉ thêm bảng. Dữ liệu dẫn xuất → xóa theo User (cascade); index không lọc
// (tránh yêu cầu QUOTED_IDENTIFIER cho mọi lệnh ghi/xóa User qua sqlcmd).
public class QuestionOutcomeConfiguration : IEntityTypeConfiguration<QuestionOutcome>
{
    public void Configure(EntityTypeBuilder<QuestionOutcome> entity)
    {
        entity.ToTable("QuestionOutcomes", t =>
        {
            t.HasCheckConstraint("CK_QuestionOutcomes_SourceKind", "[SourceKind] IN ('exam','practice','mock')");
            t.HasCheckConstraint("CK_QuestionOutcomes_Classification", "[ClassificationSource] IN ('snapshot','current')");
            t.HasCheckConstraint("CK_QuestionOutcomes_TagIdsJson", "ISJSON([TagIdsJson]) = 1");
            t.HasCheckConstraint("CK_QuestionOutcomes_SelectedJson", "ISJSON([SelectedAnswerIdsJson]) = 1");
        });
        entity.HasKey(x => new { x.SourceKind, x.SourceId, x.SnapshotQuestionId });
        entity.Property(x => x.SourceKind).HasMaxLength(20).IsRequired();
        entity.Property(x => x.ClassificationSource).HasMaxLength(20).IsRequired();
        entity.Property(x => x.TagIdsJson).IsRequired();
        entity.Property(x => x.SelectedAnswerIdsJson).IsRequired();
        entity.Property(x => x.AnswerText).HasMaxLength(200);   // To-be T16
        // Phân tích học viên: lọc UserId + SubjectId + cửa sổ ngày; INCLUDE các cột đọc để không key lookup (review T11)
        entity.HasIndex(x => new { x.UserId, x.SubjectId, x.FinishedAtUtc }).HasDatabaseName("IX_QuestionOutcomes_User_Subject_Finished")
            .IncludeProperties(x => new { x.SourceKind, x.SourceId, x.ChapterId, x.TagIdsJson, x.IsCorrect, x.ClassificationSource });
        entity.HasIndex(x => x.ExamScheduleId).HasDatabaseName("IX_QuestionOutcomes_Schedule");
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
