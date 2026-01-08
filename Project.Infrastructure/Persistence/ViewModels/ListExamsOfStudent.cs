using System.ComponentModel.DataAnnotations;

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class ListExamsOfStudent
    {
        [Key]
        public Guid UserId { get; set; }
        public int ClassId { get; set; }
        public int ExamId { get; set; }
        public string ExamName { get; set; }
        public int Duration { get; set; }
        public int TotalQuestion { get; set; }
        public int Limit { get; set; }
        public int ExamScheduleId { get; set; }
        public string ExamScheduleName { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public DateTime ExamScheduleStartTime { get; set; }
        public DateTime ExamScheduleEndTime { get; set; }
        public byte? Status { get; set; }


    }
}
