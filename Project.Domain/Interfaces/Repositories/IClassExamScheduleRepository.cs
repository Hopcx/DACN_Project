using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IClassExamScheduleRepository
    {
        Task<List<ClassExamSchedule>> GetAllClassExamSchedulesAsync();
        Task<ClassExamSchedule> GetClassExamScheduleByIdAsync(int id);
        Task<ClassExamSchedule> CreateClassExamScheduleAsync(ClassExamSchedule classExamSchedule);
        Task<ClassExamSchedule> UpdateClassExamScheduleAsync(int id, ClassExamSchedule classExamSchedule);
        Task<ClassExamSchedule> DeleteClassExamScheduleAsync(int id);
    }
}
