using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class SubmissionRepository : ISubmissionRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public SubmissionRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Submission>> GetAllSubmissionsAsync()
        {
            return await _context.Submissions.ToListAsync();
        }

        public async Task<Submission> GetSubmissionByIdAsync(int id)
        {
            return await _context.Submissions.FindAsync(id);
        }

        public async Task<List<Submission>> GetSubmissionsByUserIdAsync(Guid userId)
        {
            return await _context.Submissions.Where(x => x.UserId == userId).ToListAsync();
        }

        public async Task<Submission> CreateSubmissionAsync(Submission submission)
        {
            try
            {
                var created = _context.Submissions.Add(submission).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Submission> UpdateSubmissionAsync(int id, Submission submission)
        {
            var existing = await _context.Submissions.FindAsync(id);
            if (existing == null)
                return null;

            existing.UserId = submission.UserId;
            existing.ExamDetailId = submission.ExamDetailId;
            existing.ExamScheduleId = submission.ExamScheduleId;
            existing.SubmitTime = submission.SubmitTime;
            existing.TimeTaken = submission.TimeTaken;
            existing.TotalMark = submission.TotalMark;
            existing.IsPassed = submission.IsPassed;
            existing.UnAnswered = submission.UnAnswered;
            existing.Answered = submission.Answered;
            existing.Note = submission.Note;
            existing.Type = submission.Type;
            existing.Status = submission.Status;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Submission> DeleteSubmissionAsync(int id)
        {
            var existing = await _context.Submissions.FindAsync(id);
            if (existing == null)
                return null;

            _context.Submissions.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
