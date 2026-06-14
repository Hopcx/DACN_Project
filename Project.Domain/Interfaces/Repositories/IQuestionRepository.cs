using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IQuestionRepository
    {
        Task<List<Question>> GetAllQuestionsAsync(string? textSearch);
        Task<Question> GetQuestionByIdAsync(int id);
        Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId);
        Task<Question> CreateQuestionAsync(Question question);
        Task<Question> UpdateQuestionAsync(int id, Question question);
        Task<Question> DeleteQuestionAsync(int id);
    }
}
