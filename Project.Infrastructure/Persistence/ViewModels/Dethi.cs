

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class Dethi
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public byte? Status { get; set; }
        public DateTime CreateDate { get; set; }
        public Guid CreateBy { get; set; }
        public DateTime UpdateDate { get; set; }
        public Guid UpdateBy { get; set; }

        public List<QuestionInExam> Questions { get; set; }
        public int IdExamDetail { get; set; }
        public int IdExamDetailQues { get; set; }
        public int? NumberOfQuestions { get; set; }
        public string? Description { get; set; }
        public double? MaximmumMark { get; set; }
        public double? PassMark { get; set; }
        public int? Duration { get; set; }
        public double DiemMoiCau { get; set; }
        public int? SoLuongCH_O_1MucDo { get; set; }


    }
}
