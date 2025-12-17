using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.RoomDTO
{
    public class RoomResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public int Capacity { get; set; }

        public string Address { get; set; } = null!;

        public bool? Status { get; set; }
    }
}
