namespace Project.Application.DTOs.ExamDTO
{
    public class ExamCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte? Status { get; set; }
        public int SubjectId { get; set; }
        public int NumberOfQuestions { get; set; }
        public double MaximmumMark { get; set; }
        public double PassMark { get; set; }
        public int Duration { get; set; }
    }
}
