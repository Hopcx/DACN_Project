using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.PermissionDTO
{
    public class PermissionCreateDto
    {
        public string Name { get; set; } = null!;
        public byte? Status { get; set; }
        public string? Description { get; set; }
    }
}
