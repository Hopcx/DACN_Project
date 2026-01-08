using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.UserDTO
{
    public class UserCreateDto
    {
        [Required(ErrorMessage = "FullName is required")]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "UserName is required")]
        [MinLength(6)]
        public string UserName { get; set; } = null!;

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        public string Address { get; set; } = null!;

        [Required]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string PasswordHash { get; set; } = null!;

        public string? AvatarUrl { get; set; }

        public bool Sex { get; set; }

        public DateTime? LastLogin { get; set; }

        [Required]
        public byte Status { get; set; }

        [Required]
        public int LevelId { get; set; }
    }
    
}
