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
            if (exam.Status == 1)
                throw new InvalidOperationException("Tạo bài thi ở trạng thái nháp trước khi công khai mã đề.");
            if (!await _context.Subjects.AnyAsync(x => x.Id == exam.SubjectId && x.Status == 1))
                throw new ArgumentException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
            if (exam.ScoreMethodId.HasValue && !await _context.ScoreMethods.AnyAsync(x => x.Id == exam.ScoreMethodId))
                throw new ArgumentException("Phương pháp tính điểm không tồn tại.");
            if (await _context.Exams.AnyAsync(x => x.SubjectId == exam.SubjectId && x.Name == exam.Name && x.Status != 255))
                throw new InvalidOperationException("Tên bài thi đã tồn tại trong môn học.");
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

            if (!await _context.Subjects.AnyAsync(x => x.Id == exam.SubjectId && x.Status == 1))
                throw new ArgumentException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
            if (exam.ScoreMethodId.HasValue && !await _context.ScoreMethods.AnyAsync(x => x.Id == exam.ScoreMethodId))
                throw new ArgumentException("Phương pháp tính điểm không tồn tại.");
            if (await _context.Exams.AnyAsync(x => x.Id != id && x.SubjectId == exam.SubjectId && x.Name == exam.Name && x.Status != 255))
                throw new InvalidOperationException("Tên bài thi đã tồn tại trong môn học.");
            var variants = await _context.ExamDetails.Include(x => x.ExamDetailQuestions)
                .Where(x => x.ExamId == id && x.Status != 255).ToListAsync();
            if (variants.Count > 0 && (existing.SubjectId != exam.SubjectId ||
                existing.NumberOfQuestions != exam.NumberOfQuestions || existing.MaximmumMark != exam.MaximmumMark ||
                existing.PassMark != exam.PassMark || existing.Duration != exam.Duration || existing.NumberOfRepeat != exam.NumberOfRepeat ||
                existing.ScoreMethodId != exam.ScoreMethodId))
                throw new InvalidOperationException("Bài thi đã có mã đề; không thể đổi cấu hình ảnh hưởng đề hoặc lượt thi.");
            if (exam.Status == 1 && !variants.Any(x => x.Status == 1 &&
                x.ExamDetailQuestions.Count == exam.NumberOfQuestions &&
                Math.Abs(x.ExamDetailQuestions.Sum(q => q.Point) - exam.MaximmumMark) < 0.000001))
                throw new InvalidOperationException("Cần ít nhất một mã đề công khai, đủ số câu và tổng điểm trước khi công khai bài thi.");
            if (existing.Status == 1 && exam.Status != 1 && await _context.Submissions.AnyAsync(x => x.ExamDetail.ExamId == id))
                throw new InvalidOperationException("Bài thi đã có kết quả; không thể đổi trạng thái.");

            existing.Name = exam.Name;
            existing.Description = exam.Description;
            existing.Status = exam.Status;
            existing.SubjectId = exam.SubjectId;
            existing.NumberOfQuestions = exam.NumberOfQuestions;
            existing.NumberOfRepeat = exam.NumberOfRepeat;
            existing.AllowViewResult = exam.AllowViewResult;
            existing.ScoreMethodId = exam.ScoreMethodId;
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

            if (await _context.ExamDetails.AnyAsync(x => x.ExamId == id && x.Status != 255) ||
                await _context.Submissions.AnyAsync(x => x.ExamDetail.ExamId == id))
                throw new InvalidOperationException("Bài thi đã có mã đề hoặc kết quả; không thể ẩn.");

            existing.Status = 255;
            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
