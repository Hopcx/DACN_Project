using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ExamDetailQuestionRepository : IExamDetailQuestionRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ExamDetailQuestionRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<ExamDetailQuestion>> GetAllExamDetailQuestionsAsync()
        {
            return await _context.ExamDetailQuestions.ToListAsync();
        }

        public async Task<ExamDetailQuestion> GetExamDetailQuestionByIdAsync(int id)
        {
            return await _context.ExamDetailQuestions.FindAsync(id);
        }

        public async Task<List<ExamDetailQuestion>> GetExamDetailQuestionsByExamDetailIdAsync(int examDetailId)
        {
            return await _context.ExamDetailQuestions.Where(x => x.ExamDetailId == examDetailId).ToListAsync();
        }

        public async Task<ExamDetailQuestion> CreateExamDetailQuestionAsync(ExamDetailQuestion examDetailQuestion)
        {
            try
            {
                var created = _context.ExamDetailQuestions.Add(examDetailQuestion).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ExamDetailQuestion> UpdateExamDetailQuestionAsync(int id, ExamDetailQuestion examDetailQuestion)
        {
            var existing = await _context.ExamDetailQuestions.FindAsync(id);
            if (existing == null)
                return null;

            existing.ExamDetailId = examDetailQuestion.ExamDetailId;
            existing.QuestionId = examDetailQuestion.QuestionId;
            existing.Point = examDetailQuestion.Point;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ExamDetailQuestion> DeleteExamDetailQuestionAsync(int id)
        {
            var existing = await _context.ExamDetailQuestions.FindAsync(id);
            if (existing == null)
                return null;

            _context.ExamDetailQuestions.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
