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
    // QuestionAnswerConfiguration
    public class QuestionAnswerConfiguration : IEntityTypeConfiguration<QuestionAnswer>
    {
        public void Configure(EntityTypeBuilder<QuestionAnswer> entity)
        {
            entity.HasIndex(e => e.AnswerId);
            entity.HasIndex(e => e.QuestionId);

            entity.HasOne(d => d.Answer)
                  .WithMany(p => p.QuestionAnswers)
                  .HasForeignKey(d => d.AnswerId);

            entity.HasOne(d => d.Question)
                  .WithMany(p => p.QuestionAnswers)
                  .HasForeignKey(d => d.QuestionId);
        }
    }

}
