using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class PermissionRepository : IPermissionRepository
    {
        private readonly ProjectDACNDbContext _context;
        public PermissionRepository(ProjectDACNDbContext context)
        {
            _context = context;
        }
        public async Task<Permission> CreatePermissionAsync(Permission permission)
        {
            try
            {
                var addPer = _context.Permissions.Add(permission).Entity;
                await _context.SaveChangesAsync();
                return addPer;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<Permission> DeletePermissionAsync(int id)
        {
            try
            {
                var objDeletePer = await _context.Permissions.FindAsync(id);

                _context.Permissions.Remove(objDeletePer);
                await _context.SaveChangesAsync();
                return objDeletePer;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<Permission>> GetAllPermissionAsync()
        {
            return await _context.Permissions.AsNoTracking().ToListAsync();
        }

        public async Task<Permission> GetPermissionByIdAsync(int id)
        {
            return await _context.Permissions.FindAsync(id);
        }

        public async Task<Permission> UpdatePermissionAsync(int id, Permission r)
        {
            try
            {
                var perUpdate = await _context.Permissions.FindAsync(id);
                if (perUpdate == null)
                    return null;

                perUpdate.Name = r.Name;
                perUpdate.Status = r.Status;
                perUpdate.Description = r.Description;
                await _context.SaveChangesAsync();
                return perUpdate;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
