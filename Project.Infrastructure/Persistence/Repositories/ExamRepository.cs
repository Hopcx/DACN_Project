using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ExamRepository : IExamRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ExamRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Exam>> GetAllExamsAsync(string? textSearch, bool? isActive)
        {
            var query = _context.Exams.AsQueryable();

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

        public async Task<Exam> GetExamByIdAsync(int id)
        {
            return await _context.Exams.FindAsync(id);
        }

        public async Task<Exam> CreateExamAsync(Exam exam)
        {
            try
            {
                var created = _context.Exams.Add(exam).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Exam> UpdateExamAsync(int id, Exam exam)
        {
            var existing = await _context.Exams.FindAsync(id);
            if (existing == null)
                return null;

            existing.Name = exam.Name;
            existing.Description = exam.Description;
            existing.Status = exam.Status;
            existing.SubjectId = exam.SubjectId;
            existing.NumberOfQuestions = exam.NumberOfQuestions;
            existing.MaximmumMark = exam.MaximmumMark;
            existing.PassMark = exam.PassMark;
            existing.Duration = exam.Duration;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Exam> DeleteExamAsync(int id)
        {
            var existing = await _context.Exams.FindAsync(id);
            if (existing == null)
                return null;

            existing.Status = 255;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
