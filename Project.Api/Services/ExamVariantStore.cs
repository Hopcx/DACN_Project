using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Project.Application.DTOs.ExamDetailDTO;
using Project.Application.Interfaces.Services;
using Project.Application.Services;
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
            // T18: câu con của đoạn đang ẩn — hoặc đoạn còn câu con bị ẩn (không bao giờ đủ đoạn) — không chọn được;
            // trả kèm đoạn để FE chọn cả đoạn
            .Where(x => x.SubjectId == exam.SubjectId && x.Status == 1 && (x.GroupId == null ||
                (x.Group!.Status == 1 && !x.Group.Questions.Any(c => c.Status != 1 && c.Status != 255))))
            .OrderBy(x => x.Id)
            .Select(x => new ExamQuestionOptionDto { Id = x.Id, Content = x.Content, QuestionLevelId = x.QuestionLevelId,
                QuestionLevelName = x.QuestionLevel == null ? null : x.QuestionLevel.Name,
                GroupId = x.GroupId, GroupOrder = x.GroupOrder, GroupTitle = x.Group == null ? null : x.Group.Title })
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

        // To-be T18 (QT18-2/3): đoạn dùng chung đi nguyên khối. Kích thước đoạn = số câu con chưa xóa của đoạn đang hoạt động.
        var groupSizes = await db.Questions.AsNoTracking()
            .Where(x => x.SubjectId == exam.SubjectId && x.GroupId != null && x.Status != 255 &&
                db.QuestionGroups.Any(g => g.Id == x.GroupId && g.Status == 1))
            .GroupBy(x => x.GroupId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        // Chọn tay: câu con phải đi cùng MỌI câu con của đoạn (FE tự chọn cả đoạn)
        var manual = await db.Questions.AsNoTracking().Where(x => selected.Contains(x.Id))
            .Select(x => new { x.Id, x.GroupId, x.GroupOrder }).ToListAsync();
        foreach (var group in manual.Where(x => x.GroupId != null).GroupBy(x => x.GroupId!.Value))
            if (!groupSizes.TryGetValue(group.Key, out var size) || size != group.Count())
                throw new ArgumentException($"GROUP_INCOMPLETE: câu thuộc đoạn dùng chung #{group.Key} phải được chọn cùng cả đoạn (đoạn đang ẩn hoặc còn câu con bị ẩn thì không chọn được).");
        var random = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
        foreach (var rule in dto.RandomSelections)
        {
            var candidates = await db.Questions.AsNoTracking()
                .Where(x => x.SubjectId == exam.SubjectId && x.Status == 1 &&
                    (rule.QuestionLevelId == null || x.QuestionLevelId == rule.QuestionLevelId) &&
                    !selected.Contains(x.Id))
                .Select(x => new { x.Id, x.GroupId, x.GroupOrder }).ToListAsync();
            // Đoạn khớp luật mức độ khi MỌI câu con có mức độ đó (thiếu câu con trong ứng viên → bỏ cả đoạn)
            var units = GroupedSelection.Units(candidates, x => x.GroupId, x => x.GroupOrder ?? 0, groupSizes);
            // Review T18: thiếu vì phải bỏ đoạn (không đủ/không khớp mức độ) thì báo mã riêng, thiếu thật thì báo chung
            var available = units.Sum(x => x.Count);
            if (available < rule.Count)
                throw new InvalidOperationException(candidates.Count >= rule.Count
                    ? $"NOT_ENOUGH_QUESTIONS_FOR_GROUPS: chỉ có {available} câu dùng được (câu lẻ + đoạn đủ câu con khớp mức độ), cần {rule.Count}."
                    : "Không đủ câu hỏi phù hợp môn và mức độ.");

            var picked = GroupedSelection.PickExact(units, rule.Count, random)
                ?? throw new InvalidOperationException($"NOT_ENOUGH_QUESTIONS_FOR_GROUPS: không ghép được đúng {rule.Count} câu " +
                    $"từ câu lẻ và đoạn dùng chung (kích thước đoạn: {string.Join(", ", units.Where(x => x.Count > 1).Select(x => x.Count).OrderBy(x => x))}).");
            selected.AddRange(picked.SelectMany(x => x).Select(x => x.Id));
        }
        // Câu con của một đoạn liền nhau theo thứ tự trong đoạn
        var placement = await db.Questions.AsNoTracking().Where(x => selected.Contains(x.Id))
            .Select(x => new { x.Id, x.GroupId, x.GroupOrder }).ToDictionaryAsync(x => x.Id);
        selected = GroupedSelection.Contiguous(selected, x => placement.GetValueOrDefault(x)?.GroupId, x => placement.GetValueOrDefault(x)?.GroupOrder ?? 0);

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
