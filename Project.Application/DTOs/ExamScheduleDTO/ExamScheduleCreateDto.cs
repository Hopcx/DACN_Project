using System;

namespace Project.Application.DTOs.ExamScheduleDTO
{
    public class ExamScheduleCreateDto
    {
        public int ExamId { get; set; }
        public string? Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Description { get; set; }
        public byte? Status { get; set; }
        public int? SubjectId { get; set; }
        public int? RoomId { get; set; }
    }
}
