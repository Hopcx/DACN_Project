using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

// To-be T1: chương, tag và liên kết câu hỏi – tag. Mọi FK Restrict: câu hỏi/môn không bị xóa vật lý kéo theo metadata.
public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> entity)
    {
        entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.HasIndex(e => new { e.SubjectId, e.Name }).IsUnique().HasFilter("[Status] <> 255");
        entity.HasOne(e => e.Subject).WithMany(s => s.Chapters)
            .HasForeignKey(e => e.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> entity)
    {
        entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
        entity.Property(e => e.NormalizedName).HasMaxLength(50).IsRequired();
        entity.HasIndex(e => e.NormalizedName).IsUnique();
    }
}

public class QuestionTagConfiguration : IEntityTypeConfiguration<QuestionTag>
{
    public void Configure(EntityTypeBuilder<QuestionTag> entity)
    {
        entity.HasKey(e => new { e.QuestionId, e.TagId });
        entity.HasIndex(e => e.TagId);
        entity.HasOne(e => e.Question).WithMany(q => q.QuestionTags)
            .HasForeignKey(e => e.QuestionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Tag).WithMany(t => t.QuestionTags)
            .HasForeignKey(e => e.TagId).OnDelete(DeleteBehavior.Restrict);
    }
}
