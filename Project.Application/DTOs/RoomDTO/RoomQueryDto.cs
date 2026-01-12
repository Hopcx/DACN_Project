using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.RoomDTO
{
    public class RoomQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public string? Name { get; set; }
        public bool? Status { get; set; }
        public int? MinCapacity { get; set; }
        public int? MaxCapacity { get; set; }
    }

}
