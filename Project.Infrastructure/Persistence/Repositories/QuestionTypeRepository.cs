using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class QuestionTypeRepository : IQuestionTypeRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public QuestionTypeRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<QuestionType>> GetAllQuestionTypesAsync()
        {
            return await _context.QuestionTypes.ToListAsync();
        }

        public async Task<QuestionType> GetQuestionTypeByIdAsync(int id)
        {
            return await _context.QuestionTypes.FindAsync(id);
        }

        public async Task<QuestionType> CreateQuestionTypeAsync(QuestionType questionType)
        {
            try
            {
                var created = _context.QuestionTypes.Add(questionType).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<QuestionType> UpdateQuestionTypeAsync(int id, QuestionType questionType)
        {
            var existing = await _context.QuestionTypes.FindAsync(id);
            if (existing == null)
                return null;

            existing.Name = questionType.Name;
            existing.Description = questionType.Description;
            existing.Status = questionType.Status;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<QuestionType> DeleteQuestionTypeAsync(int id)
        {
            var existing = await _context.QuestionTypes.FindAsync(id);
            if (existing == null)
                return null;

            _context.QuestionTypes.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
