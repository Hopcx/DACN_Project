

using Project.Domain.Entities;

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class QuestionAndAnswerChoosen
    {
        public int QuestionId { get; set; }

        public List<Answer> Answers { get; set; }
    }
}
