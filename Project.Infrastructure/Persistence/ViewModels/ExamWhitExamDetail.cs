namespace Project.Infrastructure.Persistence.ViewModels
{
    public class ExamWhitExamDetail
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int IdExamDetail { get; set; }
        public int IdExamDetailQues { get; set; }
        public int NumberOfQuestions { get; set; }
        public byte Status { get; set; } = 1;
        public double MaximmumMark { get; set; }
        public double PassMark { get; set; }
        public int Duration { get; set; }

        public DateTime CreateDate { get; set; }
        public Guid CreateBy { get; set; }
        public DateTime UpdateDate { get; set; }
        public Guid UpdateBy { get; set; }

        public double Point { get; set; }

    }
}
