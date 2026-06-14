namespace Project.Application.DTOs.QuestionLevelDTO
{
    public class QuestionLevelCreateDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool? Status { get; set; }
    }
}
