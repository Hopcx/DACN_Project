namespace Project.Application.DTOs.ClassDTO
{
    public class ClassCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Capacity { get; set; }
        public Guid TeacherId { get; set; }
        public int? SubjectId { get; set; }
        public byte? Status { get; set; }
    }
}
