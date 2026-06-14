using System;

namespace Project.Application.DTOs.SubjectDTO
{
    public class SubjectResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public byte? Status { get; set; }
    }
}
