using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.LevelDTO
{
    public class UserLevelResponseDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public byte? Status { get; set; }
        public int LevelId { get; set; }
    }
}
