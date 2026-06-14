using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IClassRepository
    {
        Task<List<Class>> GetAllClassesAsync(string? textSearch);
        Task<Class> GetClassByIdAsync(int id);
        Task<Class> CreateClassAsync(Class @class);
        Task<Class> UpdateClassAsync(int id, Class @class);
        Task<Class> DeleteClassAsync(int id);
    }
}
