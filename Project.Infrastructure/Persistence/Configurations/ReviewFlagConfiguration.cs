using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

// To-be T8: đánh dấu câu. Chỉ thêm bảng.
public class ReviewFlagConfiguration : IEntityTypeConfiguration<ReviewFlag>
{
    public void Configure(EntityTypeBuilder<ReviewFlag> entity)
    {
        entity.ToTable("ReviewFlags", t => t.HasCheckConstraint("CK_ReviewFlags_SourceKind", "[SourceKind] IN ('exam','practice')"));
        entity.HasKey(x => new { x.UserId, x.SourceKind, x.SourceId, x.SnapshotQuestionId });
        entity.Property(x => x.SourceKind).HasMaxLength(20).IsRequired();
        entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
