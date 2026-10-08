using System;

namespace Project.Application.DTOs.ExamScheduleDTO
{
    public class ExamScheduleResponseDto
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string? Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TimeZoneStatus { get; set; } = "unknown";
        public bool HasAttempts { get; set; }
        public string? Description { get; set; }
        public byte? Status { get; set; }
        public int? SubjectId { get; set; }
        public int? RoomId { get; set; }
    }
}
