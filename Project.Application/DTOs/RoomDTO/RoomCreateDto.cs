using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.RoomDTO
{
    public class RoomCreateDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(200)]
        public string Name { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int Capacity { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string Address { get; set; } = null!;

        public bool? Status { get; set; }
    }
}
