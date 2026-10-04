using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ClassRepository : IClassRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ClassRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Class>> GetAllClassesAsync(string? textSearch)
        {
            var query = _context.Classes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                query = query.Where(x => x.Name.Contains(textSearch.Trim()) || x.ClassCode.Contains(textSearch.Trim()));
            }

            return await query.ToListAsync();
        }

        public async Task<Class> GetClassByIdAsync(int id)
        {
            return await _context.Classes.FindAsync(id);
        }

        public async Task<Class> CreateClassAsync(Class @class)
        {
            var created = _context.Classes.Add(@class).Entity;
            await _context.SaveChangesAsync();
            return created;
        }

        public async Task<Class> UpdateClassAsync(int id, Class @class)
        {
            var existing = await _context.Classes.FindAsync(id);
            if (existing == null)
                return null;

            existing.Name = @class.Name;
            existing.ClassCode = @class.ClassCode;
            existing.Description = @class.Description;
            existing.Capacity = @class.Capacity;
            existing.TeacherId = @class.TeacherId;
            existing.SubjectId = @class.SubjectId;
            existing.Status = @class.Status;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Class> DeleteClassAsync(int id)
        {
            var existing = await _context.Classes.FindAsync(id);
            if (existing == null)
                return null;

            existing.Status = 255;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
