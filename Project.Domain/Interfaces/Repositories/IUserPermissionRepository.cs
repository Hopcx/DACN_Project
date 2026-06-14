using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IUserPermissionRepository
    {
        Task<List<UserPermission>> GetAllUserPermissionsAsync();
        Task<UserPermission> GetUserPermissionByIdAsync(int id);
        Task<List<UserPermission>> GetUserPermissionsByUserIdAsync(Guid userId);
        Task<UserPermission> CreateUserPermissionAsync(UserPermission userPermission);
        Task<UserPermission> UpdateUserPermissionAsync(int id, UserPermission userPermission);
        Task<UserPermission> DeleteUserPermissionAsync(int id);
    }
}
