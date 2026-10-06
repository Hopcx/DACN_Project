namespace Project.Application.DTOs.ExamDTO
{
    public class ExamResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte? Status { get; set; }
        public int SubjectId { get; set; }
        public int NumberOfQuestions { get; set; }
        public int NumberOfRepeat { get; set; }
        public bool? AllowViewResult { get; set; }
        public int? ScoreMethodId { get; set; }
        public double MaximmumMark { get; set; }
        public double PassMark { get; set; }
        public int Duration { get; set; }
    }
}
