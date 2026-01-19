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
    // ClassUserConfiguration
    public class ClassUserConfiguration : IEntityTypeConfiguration<ClassUser>
    {
        public void Configure(EntityTypeBuilder<ClassUser> entity)
        {
            entity.HasIndex(e => e.ClassId);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(d => d.Class)
                  .WithMany(p => p.ClassUsers)
                  .HasForeignKey(d => d.ClassId);

            entity.HasOne(d => d.User)
                  .WithMany(p => p.ClassUsers)
                  .HasForeignKey(d => d.UserId);
        }
    }

}
