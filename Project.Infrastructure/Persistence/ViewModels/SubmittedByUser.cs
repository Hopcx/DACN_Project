namespace Project.Infrastructure.Persistence.ViewModels
{
    public class SubmittedByUser
    {
        public Guid UserId { get; set; }

        public int SubmissionId { get; set; }

        public int ExamScheduleId { get; set; }

        public string NameExam { get; set; }

        public DateTime ExamDate { get; set; }

        public string SubjectName { get; set; }

        public double Score { get; set; }

        public string ClassName { get; set; }
        public int ExamDetailId { get; set; }
        public DateTime SubmitTime { get; set; }
    }
}
