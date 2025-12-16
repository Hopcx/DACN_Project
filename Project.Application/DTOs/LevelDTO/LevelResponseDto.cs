using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.LevelDTO
{
    public class LevelResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public byte? Status { get; set; }
    }

}
