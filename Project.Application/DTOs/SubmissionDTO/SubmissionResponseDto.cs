namespace Project.Application.DTOs.SubmissionDTO
{
    public class SubmissionResponseDto
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int ExamDetailId { get; set; }
        public int ExamScheduleId { get; set; }
        public DateTime SubmitTime { get; set; }
        public TimeOnly TimeTaken { get; set; }
        public double TotalMark { get; set; }
        public bool IsPassed { get; set; }
        public int UnAnswered { get; set; }
        public int Answered { get; set; }
        public string? Note { get; set; }
        public byte? Type { get; set; }
        public bool? Status { get; set; }
    }
}
