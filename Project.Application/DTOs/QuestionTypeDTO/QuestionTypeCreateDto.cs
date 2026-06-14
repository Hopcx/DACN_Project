namespace Project.Application.DTOs.QuestionTypeDTO
{
    public class QuestionTypeCreateDto
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public bool? Status { get; set; }
    }
}
