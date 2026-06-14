using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IQuestionTypeRepository
    {
        Task<List<QuestionType>> GetAllQuestionTypesAsync();
        Task<QuestionType> GetQuestionTypeByIdAsync(int id);
        Task<QuestionType> CreateQuestionTypeAsync(QuestionType questionType);
        Task<QuestionType> UpdateQuestionTypeAsync(int id, QuestionType questionType);
        Task<QuestionType> DeleteQuestionTypeAsync(int id);
    }
}
