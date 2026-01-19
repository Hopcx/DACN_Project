using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Seed
{
    public static class UserSeed
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    FullName = "Nguyen Van A",
                    UserName = "nva",
                    Email = "abcde@gmail.com",
                    PhoneNumber = "0987654321",
                    PasswordHash = "4297f44b13955235245b2497399d7a93",
                    Status = 1,
                    LevelId = 4,
                    Sex = false,
                    Address = "A",
                    DateOfBirth = DateTime.Parse("2000-01-01")
                }
            );
        }
    }

}
