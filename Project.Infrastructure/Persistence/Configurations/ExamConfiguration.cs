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
    // ExamConfiguration
    public class ExamConfiguration : IEntityTypeConfiguration<Exam>
    {
        public void Configure(EntityTypeBuilder<Exam> entity)
        {
            entity.HasIndex(e => e.ExamScheduleId);
            entity.HasIndex(e => e.ScoreMethodId);
            entity.HasIndex(e => e.SubjectId);

            entity.HasOne(d => d.ExamSchedule)
                  .WithMany(p => p.Exams)
                  .HasForeignKey(d => d.ExamScheduleId);

            entity.HasOne(d => d.ScoreMethod)
                  .WithMany(p => p.Exams)
                  .HasForeignKey(d => d.ScoreMethodId);

            entity.HasOne(d => d.Subject)
                  .WithMany(p => p.Exams)
                  .HasForeignKey(d => d.SubjectId);
        }
    }

}
