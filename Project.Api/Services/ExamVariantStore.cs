using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Project.Application.DTOs.ExamDetailDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Services;

public sealed class ExamVariantStore(ProjectDACNDbContext db) : IExamVariantService
{
    public Task<List<ExamSubjectOptionDto>> GetSubjectsAsync() =>
        db.Subjects.AsNoTracking().Where(x => x.Status == 1).OrderBy(x => x.Name)
            .Select(x => new ExamSubjectOptionDto { Id = x.Id, Name = x.Name, Status = x.Status }).ToListAsync();

    public async Task<List<ExamQuestionOptionDto>> GetQuestionOptionsAsync(int examId)
    {
        var exam = await db.Exams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == examId && x.Status != 255)
            ?? throw new KeyNotFoundException("Bài thi không tồn tại.");
        return await db.Questions.AsNoTracking()
            .Where(x => x.SubjectId == exam.SubjectId && x.Status == 1)
            .OrderBy(x => x.Id)
            .Select(x => new ExamQuestionOptionDto { Id = x.Id, Content = x.Content, QuestionLevelId = x.QuestionLevelId,
                QuestionLevelName = x.QuestionLevel == null ? null : x.QuestionLevel.Name })
            .ToListAsync();
    }
    public async Task<List<ExamVariantResponseDto>> GetByExamAsync(int examId) =>
        (await db.ExamDetails.AsNoTracking().Include(x => x.ExamDetailQuestions)
            .Where(x => x.ExamId == examId && x.Status != 255).OrderBy(x => x.Id).ToListAsync())
        .Select(Map).ToList();

    public async Task<ExamVariantResponseDto?> GetAsync(int examId, int id)
    {
        var item = await db.ExamDetails.AsNoTracking().Include(x => x.ExamDetailQuestions)
            .FirstOrDefaultAsync(x => x.ExamId == examId && x.Id == id && x.Status != 255);
        return item == null ? null : Map(item);
    }

    public async Task<ExamVariantResponseDto> SaveAsync(int examId, int? id, ExamVariantSaveDto dto, Guid actorId)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Code) ||
            dto.Status is not (1 or 2) || dto.QuestionIds is null || dto.RandomSelections is null ||
            dto.QuestionIds.Any(x => x <= 0) || dto.RandomSelections.Any(x => x.Count <= 0 || x.QuestionLevelId < 1))
            throw new ArgumentException("Dữ liệu mã đề không hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var exam = await db.Exams.FirstOrDefaultAsync(x => x.Id == examId && x.Status != 255)
            ?? throw new KeyNotFoundException("Bài thi không tồn tại.");
        if (exam.NumberOfQuestions <= 0 || !double.IsFinite(exam.MaximmumMark) || exam.MaximmumMark <= 0)
            throw new InvalidOperationException("Cấu hình số câu hoặc điểm tối đa không hợp lệ.");
        if (exam.Status == 1)
            throw new InvalidOperationException("Bài thi đã công khai; không thể thay đổi mã đề.");

        var code = dto.Code.Trim();
        if (await db.ExamDetails.AnyAsync(x => x.ExamId == examId && x.Id != id && x.Status != 255 && x.Code == code))
            throw new InvalidOperationException("Mã đề đã tồn tại trong bài thi.");

        ExamDetail? detail = null;
        if (id.HasValue)
        {
            detail = await db.ExamDetails.Include(x => x.ExamDetailQuestions)
                .FirstOrDefaultAsync(x => x.Id == id && x.ExamId == examId && x.Status != 255)
                ?? throw new KeyNotFoundException("Mã đề không tồn tại.");
            if (detail.Status == 1 || await db.Submissions.AnyAsync(x => x.ExamDetailId == id) ||
                await db.DoingExams.AnyAsync(x => x.ExamDetailId == id))
                throw new InvalidOperationException("Mã đề đã công khai hoặc có lượt thi; không thể sửa tập câu hỏi.");
        }

        var selected = dto.QuestionIds.ToList();
        if (selected.Count != selected.Distinct().Count())
            throw new ArgumentException("Câu hỏi bị chọn trùng.");
        if (selected.Count + dto.RandomSelections.Sum(x => (long)x.Count) != exam.NumberOfQuestions)
            throw new ArgumentException("Số câu hỏi phải bằng cấu hình bài thi.");

        foreach (var rule in dto.RandomSelections)
        {
            var candidates = await db.Questions.AsNoTracking()
                .Where(x => x.SubjectId == exam.SubjectId && x.Status == 1 &&
                    (rule.QuestionLevelId == null || x.QuestionLevelId == rule.QuestionLevelId) &&
                    !selected.Contains(x.Id))
                .Select(x => x.Id).ToListAsync();
            if (candidates.Count < rule.Count)
                throw new InvalidOperationException("Không đủ câu hỏi phù hợp môn và mức độ.");
            for (var i = 0; i < rule.Count; i++)
            {
                var at = RandomNumberGenerator.GetInt32(i, candidates.Count);
                (candidates[i], candidates[at]) = (candidates[at], candidates[i]);
                selected.Add(candidates[i]);
            }
        }

        var validCount = await db.Questions.CountAsync(x => selected.Contains(x.Id) &&
            x.SubjectId == exam.SubjectId && x.Status == 1);
        if (validCount != selected.Count)
            throw new ArgumentException("Câu hỏi không tồn tại, bị ẩn hoặc khác môn học.");

        var now = DateTime.UtcNow;
        if (detail == null)
        {
            detail = new ExamDetail { ExamId = examId, CreateBy = actorId, CreateDate = now };
            db.ExamDetails.Add(detail);
        }
        else
        {
            db.ExamDetailQuestions.RemoveRange(detail.ExamDetailQuestions);
            detail.ExamDetailQuestions.Clear();
        }
        detail.Code = code;
        detail.Status = dto.Status;
        detail.UpdateBy = actorId;
        detail.UpdateDate = now;
        var point = exam.MaximmumMark / exam.NumberOfQuestions;
        foreach (var questionId in selected)
            detail.ExamDetailQuestions.Add(new ExamDetailQuestion { QuestionId = questionId, Point = point });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Map(detail);
    }

    private static ExamVariantResponseDto Map(ExamDetail x) => new()
    {
        Id = x.Id, ExamId = x.ExamId, Code = x.Code, Status = x.Status,
        Questions = x.ExamDetailQuestions.OrderBy(q => q.Id)
            .Select(q => new ExamVariantQuestionDto { Id = q.Id, QuestionId = q.QuestionId, Point = q.Point }).ToList()
    };
}
