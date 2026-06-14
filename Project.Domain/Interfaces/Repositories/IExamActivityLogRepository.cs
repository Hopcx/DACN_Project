using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IExamActivityLogRepository
    {
        Task<List<ExamActivityLog>> GetAllExamActivityLogsAsync();
        Task<ExamActivityLog> GetExamActivityLogByIdAsync(int id);
        Task<List<ExamActivityLog>> GetExamActivityLogsByExamDetailIdAsync(int examDetailId);
        Task<ExamActivityLog> CreateExamActivityLogAsync(ExamActivityLog examActivityLog);
        Task<ExamActivityLog> UpdateExamActivityLogAsync(int id, ExamActivityLog examActivityLog);
        Task<ExamActivityLog> DeleteExamActivityLogAsync(int id);
    }
}
