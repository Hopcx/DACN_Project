using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IExamDetailQuestionRepository
    {
        Task<List<ExamDetailQuestion>> GetAllExamDetailQuestionsAsync();
        Task<ExamDetailQuestion> GetExamDetailQuestionByIdAsync(int id);
        Task<List<ExamDetailQuestion>> GetExamDetailQuestionsByExamDetailIdAsync(int examDetailId);
        Task<ExamDetailQuestion> CreateExamDetailQuestionAsync(ExamDetailQuestion examDetailQuestion);
        Task<ExamDetailQuestion> UpdateExamDetailQuestionAsync(int id, ExamDetailQuestion examDetailQuestion);
        Task<ExamDetailQuestion> DeleteExamDetailQuestionAsync(int id);
    }
}
