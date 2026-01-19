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
    // ExamDetailQuestionConfiguration
    public class ExamDetailQuestionConfiguration : IEntityTypeConfiguration<ExamDetailQuestion>
    {
        public void Configure(EntityTypeBuilder<ExamDetailQuestion> entity)
        {
            entity.HasIndex(e => e.ExamDetailId);
            entity.HasIndex(e => e.QuestionId);

            entity.HasOne(d => d.ExamDetail)
                  .WithMany(p => p.ExamDetailQuestions)
                  .HasForeignKey(d => d.ExamDetailId);

            entity.HasOne(d => d.Question)
                  .WithMany(p => p.ExamDetailQuestions)
                  .HasForeignKey(d => d.QuestionId);
        }
    }

}
