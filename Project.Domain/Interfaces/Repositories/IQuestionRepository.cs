using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IQuestionRepository
    {
        Task<List<Question>> GetAllQuestionsAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId);
        Task<Question> GetQuestionByIdAsync(int id);
        Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId);
        Task<Question> CreateQuestionAsync(Question question);
        Task<Question> UpdateQuestionAsync(int id, Question question);
        Task<Question> DeleteQuestionAsync(int id);
        Task<Question?> SaveWithAnswersAsync(int? id, Question question, IReadOnlyList<Answer> answers);
        Task<bool> ReferencesExistAsync(int subjectId, int questionTypeId, int? questionLevelId);
        Task<List<Subject>> GetAvailableSubjectsAsync();
    }
}
