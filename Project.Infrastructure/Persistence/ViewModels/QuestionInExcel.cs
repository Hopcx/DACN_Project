

using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class QuestionInExcel
    {
        public int QuestionId { get; set; }
        public string Content { get; set; }
        public int QuestionLevelId { get; set; }
        public int QuestionTypeId { get; set; }
        public int SubjectId { get; set; }
        public string? ErorrMessage { get; set; }
        public bool? PassFail { get; set; }
        public List<Answer> Answers { get; set; }
    }
}
