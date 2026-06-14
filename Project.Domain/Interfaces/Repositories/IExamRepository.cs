using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IExamRepository
    {
        Task<List<Exam>> GetAllExamsAsync(string? textSearch, bool? isActive);
        Task<Exam> GetExamByIdAsync(int id);
        Task<Exam> CreateExamAsync(Exam exam);
        Task<Exam> UpdateExamAsync(int id, Exam exam);
        Task<Exam> DeleteExamAsync(int id);
    }
}
