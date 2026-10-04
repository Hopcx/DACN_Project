namespace Project.Application.DTOs.QuestionDTO
{
    public class QuestionCreateDto
    {
        public string Content { get; set; } = string.Empty;
        public byte? Status { get; set; }
        public int SubjectId { get; set; }
        public string? DocumentPath { get; set; }
        public int QuestionTypeId { get; set; }
        public int? QuestionLevelId { get; set; }
        public List<QuestionAnswerInputDto> Answers { get; set; } = new();
    }

    public class QuestionAnswerInputDto
    {
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
