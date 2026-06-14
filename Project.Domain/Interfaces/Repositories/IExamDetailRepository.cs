using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IExamDetailRepository
    {
        Task<List<ExamDetail>> GetAllExamDetailsAsync();
        Task<ExamDetail> GetExamDetailByIdAsync(int id);
        Task<List<ExamDetail>> GetExamDetailsByExamIdAsync(int examId);
        Task<ExamDetail> CreateExamDetailAsync(ExamDetail examDetail);
        Task<ExamDetail> UpdateExamDetailAsync(int id, ExamDetail examDetail);
        Task<ExamDetail> DeleteExamDetailAsync(int id);
    }
}
