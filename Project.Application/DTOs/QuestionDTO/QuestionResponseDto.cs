namespace Project.Application.DTOs.QuestionDTO
{
    public class QuestionResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public byte? Status { get; set; }
        public int SubjectId { get; set; }
        public string? DocumentPath { get; set; }
        public int QuestionTypeId { get; set; }
        public int? QuestionLevelId { get; set; }
        public bool IsUsedInExam { get; set; }
        public List<QuestionAnswerResponseDto> Answers { get; set; } = new();
    }

    public class QuestionAnswerResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class QuestionSubjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
