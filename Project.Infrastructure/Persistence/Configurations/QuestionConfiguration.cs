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
    // QuestionConfiguration
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> entity)
        {
            entity.HasIndex(e => e.QuestionLevelId);
            entity.HasIndex(e => e.QuestionTypeId);

            entity.HasOne(d => d.QuestionLevel)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(d => d.QuestionLevelId);

            entity.HasOne(d => d.QuestionType)
                  .WithMany(p => p.Questions)
                  .HasForeignKey(d => d.QuestionTypeId);
        }
    }

}
