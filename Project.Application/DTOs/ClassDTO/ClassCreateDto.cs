namespace Project.Application.DTOs.ClassDTO
{
    public class ClassCreateDto
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(200)]
        public string Name { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100)]
        public string ClassCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int Capacity { get; set; }
        public Guid TeacherId { get; set; }
        public int? SubjectId { get; set; }
        public byte? Status { get; set; }
    }
}
