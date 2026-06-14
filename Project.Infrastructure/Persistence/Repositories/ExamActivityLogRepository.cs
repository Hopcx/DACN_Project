using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ExamActivityLogRepository : IExamActivityLogRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ExamActivityLogRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<ExamActivityLog>> GetAllExamActivityLogsAsync()
        {
            return await _context.ExamActivityLogs.ToListAsync();
        }

        public async Task<ExamActivityLog> GetExamActivityLogByIdAsync(int id)
        {
            return await _context.ExamActivityLogs.FindAsync(id);
        }

        public async Task<List<ExamActivityLog>> GetExamActivityLogsByExamDetailIdAsync(int examDetailId)
        {
            return await _context.ExamActivityLogs.Where(x => x.ExamDetailId == examDetailId).ToListAsync();
        }

        public async Task<ExamActivityLog> CreateExamActivityLogAsync(ExamActivityLog examActivityLog)
        {
            try
            {
                var created = _context.ExamActivityLogs.Add(examActivityLog).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ExamActivityLog> UpdateExamActivityLogAsync(int id, ExamActivityLog examActivityLog)
        {
            var existing = await _context.ExamActivityLogs.FindAsync(id);
            if (existing == null)
                return null;

            existing.ExamDetailId = examActivityLog.ExamDetailId;
            existing.UserId = examActivityLog.UserId;
            existing.ActionTime = examActivityLog.ActionTime;
            existing.ActionType = examActivityLog.ActionType;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ExamActivityLog> DeleteExamActivityLogAsync(int id)
        {
            var existing = await _context.ExamActivityLogs.FindAsync(id);
            if (existing == null)
                return null;

            _context.ExamActivityLogs.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
