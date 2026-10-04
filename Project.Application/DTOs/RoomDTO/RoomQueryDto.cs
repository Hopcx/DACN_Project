using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.RoomDTO
{
    public class RoomQueryDto
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;
        [System.ComponentModel.DataAnnotations.Range(1, 100)]
        public int PageSize { get; set; } = 10;

        public string? Name { get; set; }
        public bool? Status { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? MinCapacity { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int? MaxCapacity { get; set; }
    }

}
