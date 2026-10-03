using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Configurations;

public class BlackListTokenConfiguration : IEntityTypeConfiguration<BlackListToken>
{
    public void Configure(EntityTypeBuilder<BlackListToken> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Token).HasMaxLength(255).IsRequired();
        entity.HasIndex(x => x.Token).IsUnique();
        entity.HasIndex(x => x.ExpiryDate);
    }
}
