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
    // ExamScheduleConfiguration
    public class ExamScheduleConfiguration : IEntityTypeConfiguration<ExamSchedule>
    {
        public void Configure(EntityTypeBuilder<ExamSchedule> entity)
        {
            entity.HasIndex(e => e.RoomId);
            entity.HasIndex(e => e.SubjectId);

            entity.HasOne(d => d.Room)
                  .WithMany(p => p.ExamSchedules)
                  .HasForeignKey(d => d.RoomId);

            entity.HasOne(d => d.Subject)
                  .WithMany(p => p.ExamSchedules)
                  .HasForeignKey(d => d.SubjectId);
        }
    }

}
