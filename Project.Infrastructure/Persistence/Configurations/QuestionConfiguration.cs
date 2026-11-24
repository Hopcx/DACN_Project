using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Configurations
{
    // QuestionConfiguration
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> entity)
        {
            entity.HasIndex(e => e.QuestionLevelId);
            entity.HasIndex(e => e.QuestionTypeId);

            entity.HasOne(d => d.QuestionLevel)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(d => d.QuestionLevelId);

            entity.HasOne(d => d.QuestionType)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(d => d.QuestionTypeId);

            // To-be T1: chương (nullable) và lời giải
            entity.Property(e => e.Explanation).HasMaxLength(4000);
            entity.HasIndex(e => e.ChapterId);
            entity.HasOne(d => d.Chapter)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(d => d.ChapterId)
                  .OnDelete(DeleteBehavior.Restrict);

            // To-be T16: tùy chọn so khớp câu điền ngắn (chỉ loại 4)
            entity.Property(e => e.ShortAnswerTolerance).HasPrecision(18, 6);
            entity.ToTable(t => t.HasCheckConstraint("CK_Questions_ShortAnswerTolerance",
                "[ShortAnswerTolerance] IS NULL OR ([ShortAnswerTolerance] >= 0 AND [ShortAnswerTolerance] <= 1000000000)"));

            // To-be T18: đoạn dùng chung (FK Restrict) — GroupId và GroupOrder cùng có hoặc cùng NULL
            entity.HasIndex(e => new { e.GroupId, e.GroupOrder }).HasDatabaseName("IX_Questions_Group");
            entity.HasOne(d => d.Group).WithMany(p => p.Questions).HasForeignKey(d => d.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Questions_Group",
                "([GroupId] IS NULL AND [GroupOrder] IS NULL) OR ([GroupId] IS NOT NULL AND [GroupOrder] IS NOT NULL)"));
        }
    }

}
