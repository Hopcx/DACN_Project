using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IClassUserRepository
    {
        Task<List<ClassUser>> GetAllClassUsersAsync();
        Task<ClassUser> GetClassUserByIdAsync(int id);
        Task<List<ClassUser>> GetClassUsersByClassIdAsync(int classId);
        Task<ClassUser> CreateClassUserAsync(ClassUser classUser);
        Task<ClassUser> UpdateClassUserAsync(int id, ClassUser classUser);
        Task<ClassUser> DeleteClassUserAsync(int id);
    }
}
