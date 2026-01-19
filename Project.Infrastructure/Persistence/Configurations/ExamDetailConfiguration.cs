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
    // ExamDetailConfiguration
    public class ExamDetailConfiguration : IEntityTypeConfiguration<ExamDetail>
    {
        public void Configure(EntityTypeBuilder<ExamDetail> entity)
        {
            entity.HasIndex(e => e.ExamId);

            entity.HasOne(d => d.Exam)
                  .WithMany(p => p.ExamDetails)
                  .HasForeignKey(d => d.ExamId);
        }
    }

}
