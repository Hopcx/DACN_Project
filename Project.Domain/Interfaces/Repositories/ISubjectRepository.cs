using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface ISubjectRepository
    {
        Task<List<Subject>> GetAllSubjectAsync(string? textSearch, bool? isActive);
        Task<Subject> GetSubjectByIdAsync(int id);
        Task<Subject> CreateSubjectAsync(Subject subject);
        Task<Subject> UpdateSubjectAsync(int id, Subject subject);
        Task<Subject> DeleteSubjectAsync(int id);
    }
}
