using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class UserPermissionRepository : IUserPermissionRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public UserPermissionRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<UserPermission>> GetAllUserPermissionsAsync()
        {
            return await _context.UserPermissions.ToListAsync();
        }

        public async Task<UserPermission> GetUserPermissionByIdAsync(int id)
        {
            return await _context.UserPermissions.FindAsync(id);
        }

        public async Task<List<UserPermission>> GetUserPermissionsByUserIdAsync(Guid userId)
        {
            return await _context.UserPermissions.Where(x => x.UserId == userId).ToListAsync();
        }

        public async Task<UserPermission> CreateUserPermissionAsync(UserPermission userPermission)
        {
            try
            {
                var created = _context.UserPermissions.Add(userPermission).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<UserPermission> UpdateUserPermissionAsync(int id, UserPermission userPermission)
        {
            var existing = await _context.UserPermissions.FindAsync(id);
            if (existing == null)
                return null;

            existing.UserId = userPermission.UserId;
            existing.PermissionId = userPermission.PermissionId;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<UserPermission> DeleteUserPermissionAsync(int id)
        {
            var existing = await _context.UserPermissions.FindAsync(id);
            if (existing == null)
                return null;

            _context.UserPermissions.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
