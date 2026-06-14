namespace Project.Application.DTOs.ExamActivityLogDTO
{
    public class ExamActivityLogResponseDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public int? ExamDetailId { get; set; }
        public int? ExamScheduleId { get; set; }
        public Guid UserId { get; set; }
        public DateTime ActionTime { get; set; }
        public string? ActionType { get; set; }
    }
}
