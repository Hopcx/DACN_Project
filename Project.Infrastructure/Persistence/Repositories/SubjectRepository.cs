using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class SubjectRepository : ISubjectRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public SubjectRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Subject>> GetAllSubjectAsync(string? textSearch, bool? isActive)
        {
            var query = _context.Subjects.AsQueryable();

            if (isActive == true)
            {
                query = query.Where(x => x.Status == 1);
            }

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                query = query.Where(x => x.Name.Contains(textSearch.Trim()));
            }

            return await query.ToListAsync();
        }

        public async Task<Subject> GetSubjectByIdAsync(int id)
        {
            return await _context.Subjects.FindAsync(id);
        }

        public async Task<Subject> CreateSubjectAsync(Subject subject)
        {
            try
            {
                var created = _context.Subjects.Add(subject).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Subject> UpdateSubjectAsync(int id, Subject subject)
        {
            var existing = await _context.Subjects.FindAsync(id);
            if (existing == null)
                return null;

            existing.Name = subject.Name;
            existing.Description = subject.Description;
            existing.Status = subject.Status;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Subject> DeleteSubjectAsync(int id)
        {
            var existing = await _context.Subjects.FindAsync(id);
            if (existing == null)
                return null;

            existing.Status = 255;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
