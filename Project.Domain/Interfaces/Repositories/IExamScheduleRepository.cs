using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IExamScheduleRepository
    {
        Task<List<ExamSchedule>> GetAllExamSchedulesAsync();
        Task<ExamSchedule> GetExamScheduleByIdAsync(int id);
        Task<ExamSchedule> CreateExamScheduleAsync(ExamSchedule examSchedule);
        Task<ExamSchedule> UpdateExamScheduleAsync(int id, ExamSchedule examSchedule);
        Task<ExamSchedule> DeleteExamScheduleAsync(int id);
    }
}
