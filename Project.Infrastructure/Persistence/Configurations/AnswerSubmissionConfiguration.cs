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
    public class AnswerSubmissionConfiguration : IEntityTypeConfiguration<AnswerSubmission>
    {
        public void Configure(EntityTypeBuilder<AnswerSubmission> entity)
        {
            entity.HasIndex(e => e.AnswerId);
            entity.HasIndex(e => e.QuestionId);
            entity.HasIndex(e => e.SubmissionId);

            entity.HasOne(d => d.Answer)
                  .WithMany(p => p.AnswerSubmissions)
                  .HasForeignKey(d => d.AnswerId);

            entity.HasOne(d => d.Question)
                  .WithMany(p => p.AnswerSubmissions)
                  .HasForeignKey(d => d.QuestionId);

            entity.HasOne(d => d.Submission)
                  .WithMany(p => p.AnswerSubmissions)
                  .HasForeignKey(d => d.SubmissionId);
        }
    }

}
