using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class QuestionLevelRepository : IQuestionLevelRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public QuestionLevelRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<QuestionLevel>> GetAllQuestionLevelsAsync(string? textSearch)
        {
            var query = _context.QuestionLevels.AsQueryable();

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                query = query.Where(x => x.Name.Contains(textSearch.Trim()));
            }

            return await query.ToListAsync();
        }

        public async Task<QuestionLevel> GetQuestionLevelByIdAsync(int id)
        {
            return await _context.QuestionLevels.FindAsync(id);
        }

        public async Task<QuestionLevel> CreateQuestionLevelAsync(QuestionLevel questionLevel)
        {
            try
            {
                var created = _context.QuestionLevels.Add(questionLevel).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<QuestionLevel> UpdateQuestionLevelAsync(int id, QuestionLevel questionLevel)
        {
            var existing = await _context.QuestionLevels.FindAsync(id);
            if (existing == null)
                return null;

            existing.Name = questionLevel.Name;
            existing.Description = questionLevel.Description;
            existing.Status = questionLevel.Status;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<QuestionLevel> DeleteQuestionLevelAsync(int id)
        {
            var existing = await _context.QuestionLevels.FindAsync(id);
            if (existing == null)
                return null;

            existing.Status = false;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
