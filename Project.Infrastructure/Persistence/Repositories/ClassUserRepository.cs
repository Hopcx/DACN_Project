using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ClassUserRepository : IClassUserRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ClassUserRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<ClassUser>> GetAllClassUsersAsync()
        {
            return await _context.ClassUsers.ToListAsync();
        }

        public async Task<ClassUser> GetClassUserByIdAsync(int id)
        {
            return await _context.ClassUsers.FindAsync(id);
        }

        public async Task<List<ClassUser>> GetClassUsersByClassIdAsync(int classId)
        {
            return await _context.ClassUsers.Where(x => x.ClassId == classId).ToListAsync();
        }

        public async Task<ClassUser> CreateClassUserAsync(ClassUser classUser)
        {
            try
            {
                var created = _context.ClassUsers.Add(classUser).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ClassUser> UpdateClassUserAsync(int id, ClassUser classUser)
        {
            var existing = await _context.ClassUsers.FindAsync(id);
            if (existing == null)
                return null;

            existing.ClassId = classUser.ClassId;
            existing.UserId = classUser.UserId;
            //existing.JoinDate = classUser.JoinDate;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ClassUser> DeleteClassUserAsync(int id)
        {
            var existing = await _context.ClassUsers.FindAsync(id);
            if (existing == null)
                return null;

            _context.ClassUsers.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
