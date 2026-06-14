using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ExamScheduleRepository : IExamScheduleRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ExamScheduleRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<ExamSchedule>> GetAllExamSchedulesAsync()
        {
            return await _context.ExamSchedules.ToListAsync();
        }

        public async Task<ExamSchedule> GetExamScheduleByIdAsync(int id)
        {
            return await _context.ExamSchedules.FindAsync(id);
        }

        public async Task<ExamSchedule> CreateExamScheduleAsync(ExamSchedule examSchedule)
        {
            try
            {
                var created = _context.ExamSchedules.Add(examSchedule).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ExamSchedule> UpdateExamScheduleAsync(int id, ExamSchedule examSchedule)
        {
            var existing = await _context.ExamSchedules.FindAsync(id);
            if (existing == null)
                return null;

            existing.ExamId = examSchedule.ExamId;
            existing.Title = examSchedule.Title;
            existing.StartTime = examSchedule.StartTime;
            existing.EndTime = examSchedule.EndTime;
            existing.Description = examSchedule.Description;
            existing.Status = examSchedule.Status;
            existing.SubjectId = examSchedule.SubjectId;
            existing.RoomId = examSchedule.RoomId;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ExamSchedule> DeleteExamScheduleAsync(int id)
        {
            var existing = await _context.ExamSchedules.FindAsync(id);
            if (existing == null)
                return null;

            existing.Status = 255;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
