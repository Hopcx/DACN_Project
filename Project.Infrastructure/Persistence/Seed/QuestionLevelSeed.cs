using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.Seed
{
    // To-be Q16: độ khó dùng bảng QuestionLevels với 3 mức hệ thống.
    public static class QuestionLevelSeed
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<QuestionLevel>().HasData(
                new QuestionLevel { Id = 1, Name = "Dễ", Description = "Câu hỏi mức độ dễ", Status = true },
                new QuestionLevel { Id = 2, Name = "Trung bình", Description = "Câu hỏi mức độ trung bình", Status = true },
                new QuestionLevel { Id = 3, Name = "Khó", Description = "Câu hỏi mức độ khó", Status = true }
            );
        }
    }
}
