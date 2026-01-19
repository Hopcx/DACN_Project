using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Seed
{
    public static class QuestionTypeSeed
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuestionType>().HasData(
                new QuestionType { Id = 1, Name = "Đúng / Sai", Description = "Chọn câu trả lời Đúng hoặc Sai", Status = true },
                new QuestionType { Id = 2, Name = "Chọn đáp án đúng nhất", Description = "Chọn đáp án đúng nhất", Status = true },
                new QuestionType { Id = 3, Name = "Chọn nhiều đáp án đúng", Description = "Chọn các đáp án đúng", Status = true }
            );
        }
    }

}
