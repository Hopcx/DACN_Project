using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Seed
{
    public static class LevelSeed
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Level>().HasData(
                new Level { Id = 1, Name = "Admin", Status = 1 },
                new Level { Id = 2, Name = "Examiner", Status = 1 },
                new Level { Id = 3, Name = "Teacher", Status = 1 },
                new Level { Id = 4, Name = "Student", Status = 1 }
            );
        }
    }

}
