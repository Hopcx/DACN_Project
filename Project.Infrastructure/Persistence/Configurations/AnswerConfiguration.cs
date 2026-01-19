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
    public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
    {
        public void Configure(EntityTypeBuilder<Answer> entity)
        {
            entity.HasIndex(e => e.QuestionId);

            entity.HasOne(d => d.Question)
                  .WithMany(p => p.Answers)
                  .HasForeignKey(d => d.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }

}
