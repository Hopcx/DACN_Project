using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

// To-be T14: media + đính kèm câu hỏi. Chỉ thêm bảng.
public class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> entity)
    {
        entity.ToTable("MediaFiles", t =>
        {
            t.HasCheckConstraint("CK_MediaFiles_Kind", "[Kind] IN ('image','audio')");
            t.HasCheckConstraint("CK_MediaFiles_Size", "[SizeBytes] > 0");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Kind).HasMaxLength(10).IsRequired();
        entity.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        entity.Property(x => x.Extension).HasMaxLength(10).IsRequired();
        entity.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        entity.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
        entity.HasIndex(x => x.Sha256).IsUnique().HasDatabaseName("UX_MediaFiles_Sha256");
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuestionMediaConfiguration : IEntityTypeConfiguration<QuestionMedia>
{
    public void Configure(EntityTypeBuilder<QuestionMedia> entity)
    {
        entity.ToTable("QuestionMedia");
        entity.HasKey(x => new { x.QuestionId, x.MediaId });
        entity.HasOne(x => x.Question).WithMany(x => x.QuestionMedia).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Media).WithMany(x => x.QuestionMedia).HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => x.MediaId).HasDatabaseName("IX_QuestionMedia_Media");
    }
}
