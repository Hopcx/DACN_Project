using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ClassExamScheduleRepository : IClassExamScheduleRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ClassExamScheduleRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<ClassExamSchedule>> GetAllClassExamSchedulesAsync()
        {
            return await _context.ClassExamSchedules.ToListAsync();
        }

        public async Task<ClassExamSchedule> GetClassExamScheduleByIdAsync(int id)
        {
            return await _context.ClassExamSchedules.FindAsync(id);
        }

        public async Task<ClassExamSchedule> CreateClassExamScheduleAsync(ClassExamSchedule classExamSchedule)
        {
            try
            {
                var created = _context.ClassExamSchedules.Add(classExamSchedule).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ClassExamSchedule> UpdateClassExamScheduleAsync(int id, ClassExamSchedule classExamSchedule)
        {
            var existing = await _context.ClassExamSchedules.FindAsync(id);
            if (existing == null)
                return null;

            existing.ClassId = classExamSchedule.ClassId;
            existing.ExamScheduleId = classExamSchedule.ExamScheduleId;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ClassExamSchedule> DeleteClassExamScheduleAsync(int id)
        {
            var existing = await _context.ClassExamSchedules.FindAsync(id);
            if (existing == null)
                return null;

            _context.ClassExamSchedules.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
