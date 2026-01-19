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
    // ExamActivityLogConfiguration
    public class ExamActivityLogConfiguration : IEntityTypeConfiguration<ExamActivityLog>
    {
        public void Configure(EntityTypeBuilder<ExamActivityLog> entity)
        {
            entity.HasIndex(e => e.ExamDetailId);
            entity.HasIndex(e => e.ExamId);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(d => d.ExamDetail)
                  .WithMany(p => p.ExamActivityLogs)
                  .HasForeignKey(d => d.ExamDetailId);

            entity.HasOne(d => d.Exam)
                  .WithMany(p => p.ExamActivityLogs)
                  .HasForeignKey(d => d.ExamId);

            entity.HasOne(d => d.User)
                  .WithMany(p => p.ExamActivityLogs)
                  .HasForeignKey(d => d.UserId);
        }
    }

}
