using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IQuestionLevelRepository
    {
        Task<List<QuestionLevel>> GetAllQuestionLevelsAsync(string? textSearch);
        Task<QuestionLevel> GetQuestionLevelByIdAsync(int id);
        Task<QuestionLevel> CreateQuestionLevelAsync(QuestionLevel questionLevel);
        Task<QuestionLevel> UpdateQuestionLevelAsync(int id, QuestionLevel questionLevel);
        Task<QuestionLevel> DeleteQuestionLevelAsync(int id);
    }
}
