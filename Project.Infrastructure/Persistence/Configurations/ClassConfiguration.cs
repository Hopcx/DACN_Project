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
    public class ClassConfiguration : IEntityTypeConfiguration<Class>
    {
        public void Configure(EntityTypeBuilder<Class> entity)
        {
            entity.HasIndex(e => e.ClassCode).IsUnique();
            entity.HasIndex(e => e.SubjectId);

            entity.HasOne(d => d.Subject)
                  .WithMany(p => p.Classes)
                  .HasForeignKey(d => d.SubjectId);
        }
    }

}
