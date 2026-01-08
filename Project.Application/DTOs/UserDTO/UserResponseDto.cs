using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.UserDTO
{
    public class UserResponseDto
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = null!;

        public string UserName { get; set; } = null!;

        public DateTime DateOfBirth { get; set; }

        public string PhoneNumber { get; set; } = null!;

        public string Address { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public string? AvatarUrl { get; set; }

        public bool Sex { get; set; }

        public DateTime? LastLogin { get; set; }

        public byte Status { get; set; }

        public int LevelId { get; set; }
    }
}
