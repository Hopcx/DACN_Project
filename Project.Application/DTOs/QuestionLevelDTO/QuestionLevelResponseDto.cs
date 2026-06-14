namespace Project.Application.DTOs.QuestionLevelDTO
{
    public class QuestionLevelResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool? Status { get; set; }
    }
}
