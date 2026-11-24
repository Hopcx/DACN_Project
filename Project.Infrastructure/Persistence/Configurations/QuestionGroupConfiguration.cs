using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

// To-be T18: đoạn dùng chung + media của đoạn
public class QuestionGroupConfiguration : IEntityTypeConfiguration<QuestionGroup>
{
    public void Configure(EntityTypeBuilder<QuestionGroup> entity)
    {
        entity.ToTable("QuestionGroups");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
        entity.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        entity.Property(x => x.Status).HasDefaultValue((byte)1);
        entity.HasIndex(x => new { x.SubjectId, x.Status }).HasDatabaseName("IX_QuestionGroups_Subject_Status");
        entity.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuestionGroupMediaConfiguration : IEntityTypeConfiguration<QuestionGroupMedia>
{
    public void Configure(EntityTypeBuilder<QuestionGroupMedia> entity)
    {
        entity.ToTable("QuestionGroupMedia");
        entity.HasKey(x => new { x.GroupId, x.MediaId });
        entity.HasOne(x => x.Group).WithMany(x => x.Media).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Media).WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.MediaId).HasDatabaseName("IX_QuestionGroupMedia_Media");
    }
}
