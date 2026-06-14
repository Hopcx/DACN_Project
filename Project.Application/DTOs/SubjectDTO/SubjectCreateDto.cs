using System;

namespace Project.Application.DTOs.SubjectDTO
{
    public class SubjectCreateDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public byte? Status { get; set; }
    }
}
