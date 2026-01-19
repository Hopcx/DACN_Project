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
    public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
    {
        public void Configure(EntityTypeBuilder<Submission> entity)
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExamDetailId);
            entity.HasIndex(e => e.ExamScheduleId);

            entity.HasOne(d => d.User)
                  .WithMany(p => p.Submissions)
                  .HasForeignKey(d => d.UserId);

            entity.HasOne(d => d.ExamDetail)
                  .WithMany(p => p.Submissions)
                  .HasForeignKey(d => d.ExamDetailId)
                  .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ExamSchedule)
                  .WithMany(p => p.Submissions)
                  .HasForeignKey(d => d.ExamScheduleId)
                  .OnDelete(DeleteBehavior.ClientSetNull);
        }
    }

}
