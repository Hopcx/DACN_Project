using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class AnswerSubmissionRepository : IAnswerSubmissionRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public AnswerSubmissionRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<AnswerSubmission>> GetAllAnswerSubmissionsAsync()
        {
            return await _context.AnswerSubmissions.ToListAsync();
        }

        public async Task<AnswerSubmission> GetAnswerSubmissionByIdAsync(int id)
        {
            return await _context.AnswerSubmissions.FindAsync(id);
        }

        public async Task<List<AnswerSubmission>> GetAnswerSubmissionsBySubmissionIdAsync(int submissionId)
        {
            return await _context.AnswerSubmissions.Where(x => x.SubmissionId == submissionId).ToListAsync();
        }

        public async Task<AnswerSubmission> CreateAnswerSubmissionAsync(AnswerSubmission answerSubmission)
        {
            try
            {
                var created = _context.AnswerSubmissions.Add(answerSubmission).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<AnswerSubmission> UpdateAnswerSubmissionAsync(int id, AnswerSubmission answerSubmission)
        {
            var existing = await _context.AnswerSubmissions.FindAsync(id);
            if (existing == null)
                return null;

            existing.SubmissionId = answerSubmission.SubmissionId;
            existing.AnswerId = answerSubmission.AnswerId;
            existing.QuestionId = answerSubmission.QuestionId;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<AnswerSubmission> DeleteAnswerSubmissionAsync(int id)
        {
            var existing = await _context.AnswerSubmissions.FindAsync(id);
            if (existing == null)
                return null;

            _context.AnswerSubmissions.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
