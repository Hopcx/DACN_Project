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
    // ClassExamScheduleConfiguration
    public class ClassExamScheduleConfiguration : IEntityTypeConfiguration<ClassExamSchedule>
    {
        public void Configure(EntityTypeBuilder<ClassExamSchedule> entity)
        {
            entity.ToTable("ClassExamSchedule");

            entity.HasIndex(e => e.ClassId);
            entity.HasIndex(e => e.ExamScheduleId);

            entity.HasOne(d => d.Class)
                  .WithMany(p => p.ClassExamSchedules)
                  .HasForeignKey(d => d.ClassId);

            entity.HasOne(d => d.ExamSchedule)
                  .WithMany(p => p.ClassExamSchedules)
                  .HasForeignKey(d => d.ExamScheduleId);
        }
    }

}
