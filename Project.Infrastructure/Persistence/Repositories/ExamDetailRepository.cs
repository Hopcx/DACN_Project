using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class ExamDetailRepository : IExamDetailRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public ExamDetailRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<ExamDetail>> GetAllExamDetailsAsync()
        {
            return await _context.ExamDetails.ToListAsync();
        }

        public async Task<ExamDetail> GetExamDetailByIdAsync(int id)
        {
            return await _context.ExamDetails.FindAsync(id);
        }

        public async Task<List<ExamDetail>> GetExamDetailsByExamIdAsync(int examId)
        {
            return await _context.ExamDetails.Where(x => x.ExamId == examId).ToListAsync();
        }

        public async Task<ExamDetail> CreateExamDetailAsync(ExamDetail examDetail)
        {
            try
            {
                var created = _context.ExamDetails.Add(examDetail).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ExamDetail> UpdateExamDetailAsync(int id, ExamDetail examDetail)
        {
            var existing = await _context.ExamDetails.FindAsync(id);
            if (existing == null)
                return null;

            existing.ExamId = examDetail.ExamId;
            existing.Code = examDetail.Code;
            existing.Status = examDetail.Status;
            existing.CreateDate = examDetail.CreateDate;
            existing.CreateBy = examDetail.CreateBy;
            existing.UpdateDate = examDetail.UpdateDate;
            existing.UpdateBy = examDetail.UpdateBy;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<ExamDetail> DeleteExamDetailAsync(int id)
        {
            var existing = await _context.ExamDetails.FindAsync(id);
            if (existing == null)
                return null;

            _context.ExamDetails.Remove(existing);
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
