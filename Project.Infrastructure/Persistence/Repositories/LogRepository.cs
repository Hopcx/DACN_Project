using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class LogRepository : ILogRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public LogRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Log>> GetAllLogsAsync()
        {
            return await _context.Logs.ToListAsync();
        }

        public async Task<Log> GetLogByIdAsync(int id)
        {
            return await _context.Logs.FindAsync(id);
        }

        public async Task<Log> CreateLogAsync(Log log)
        {
            try
            {
                var created = _context.Logs.Add(log).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Log> UpdateLogAsync(int id, Log log)
        {
            var existing = await _context.Logs.FindAsync(id);
            if (existing == null)
                return null;

            existing.Id = log.Id;
            existing.Name = log.Name;
            existing.Description = log.Description;
            existing.Status = log.Status;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Log> DeleteLogAsync(int id)
        {
            var existing = await _context.Logs.FindAsync(id);
            if (existing == null)
                return null;

            _context.Logs.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
