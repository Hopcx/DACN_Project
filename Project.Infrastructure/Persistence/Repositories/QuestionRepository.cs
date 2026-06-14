using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public QuestionRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Question>> GetAllQuestionsAsync(string? textSearch)
        {
            var query = _context.Questions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                query = query.Where(x => x.Content.Contains(textSearch.Trim()));
            }

            return await query.ToListAsync();
        }

        public async Task<Question> GetQuestionByIdAsync(int id)
        {
            return await _context.Questions.FindAsync(id);
        }

        public async Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId)
        {
            return await _context.Questions.Where(x => x.SubjectId == subjectId).ToListAsync();
        }

        public async Task<Question> CreateQuestionAsync(Question question)
        {
            try
            {
                var created = _context.Questions.Add(question).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Question> UpdateQuestionAsync(int id, Question question)
        {
            var existing = await _context.Questions.FindAsync(id);
            if (existing == null)
                return null;

            existing.Content = question.Content;
            existing.Status = question.Status;
            existing.SubjectId = question.SubjectId;
            existing.DocumentPath = question.DocumentPath;
            existing.QuestionTypeId = question.QuestionTypeId;
            existing.QuestionLevelId = question.QuestionLevelId;
            existing.UpdatedBy = question.UpdatedBy;
            existing.UpdatedAt = question.UpdatedAt;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Question> DeleteQuestionAsync(int id)
        {
            var existing = await _context.Questions.FindAsync(id);
            if (existing == null)
                return null;

            existing.Status = 255;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
