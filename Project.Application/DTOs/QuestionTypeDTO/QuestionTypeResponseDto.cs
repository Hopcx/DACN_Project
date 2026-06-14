namespace Project.Application.DTOs.QuestionTypeDTO
{
    public class QuestionTypeResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool? Status { get; set; }
    }
}
