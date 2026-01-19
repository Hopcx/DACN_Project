using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Seed
{
    public static class PermissionSeed
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "Quản lý bài thi", Description = "Quản lý bài thi", Status = 1 },
                new Permission { Id = 2, Name = "Quản lý câu hỏi và đáp án", Description = "Quản lý câu hỏi và đáp án", Status = 1 },
                new Permission { Id = 3, Name = "Quản lý môn học", Description = "Quản lý môn học", Status = 1 },
                new Permission { Id = 4, Name = "Quản lý lịch thi", Description = "Quản lý lịch thi", Status = 1 }
            );
        }
    }

}
