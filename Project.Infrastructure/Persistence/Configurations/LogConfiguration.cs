using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations
{
    // P4 audit columns. Không có FK tới Users: xóa user là hard delete và audit phải còn lại.
    public class LogConfiguration : IEntityTypeConfiguration<Log>
    {
        public void Configure(EntityTypeBuilder<Log> entity)
        {
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);

            entity.HasIndex(e => new { e.CreatedAtUtc, e.Id });
            entity.HasIndex(e => e.ActorUserId);
        }
    }
}
