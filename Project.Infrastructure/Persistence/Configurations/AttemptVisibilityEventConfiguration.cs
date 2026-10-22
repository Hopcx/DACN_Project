using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

public class AttemptVisibilityEventConfiguration : IEntityTypeConfiguration<AttemptVisibilityEvent>
{
    public void Configure(EntityTypeBuilder<AttemptVisibilityEvent> entity)
    {
        entity.ToTable("AttemptVisibilityEvents");
        entity.HasKey(x => new { x.AttemptId, x.EventId });
        entity.HasIndex(x => new { x.AttemptId, x.RecordedAtUtc });
        entity.HasOne(x => x.Attempt).WithMany().HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
