using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Api.Services;
using Project.Application.Common;
using Project.Application.DTOs.QuestionDTO;
using Project.Application.Exceptions;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

// P4/F12: xuất câu hỏi của một môn ra workbook v1 mà QuestionImportController nhận lại được.
// Kiểm toàn bộ trước; có câu không import lại được thì 409 kèm danh sách, không xuất file.
[ApiController]
[Authorize(Policy = "QuestionManagement")]
[Route("web/questions/export")]
public class QuestionExportController : ControllerBase
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const int MaxRows = 500;
    private readonly ProjectDACNDbContext _db;
    private readonly AuditWriter _audit;
    public QuestionExportController(ProjectDACNDbContext db, AuditWriter audit) { _db = db; _audit = audit; }

    private sealed record ExportIssue(int QuestionId, string Code, string Message);

    // Cặp surrogate hợp lệ (ví dụ emoji) là ký tự XML hợp lệ; chỉ nửa surrogate lẻ hoặc ký tự điều khiển mới bị loại.
    private static bool HasInvalidXmlChar(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) { i++; continue; }
            if (!XmlConvert.IsXmlChar(value[i])) return true;
        }
        return false;
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] int? subjectId, [FromQuery] int? questionTypeId,
        [FromQuery] int? questionLevelId)
    {
        if (subjectId is null or <= 0 || questionTypeId is <= 0 || questionLevelId is <= 0)
            return BadRequest(ApiResponse<string>.Fail("INVALID_FILTER"));
        if (!await _db.Subjects.AsNoTracking().AnyAsync(x => x.Id == subjectId))
            return NotFound(ApiResponse<string>.Fail("SUBJECT_NOT_FOUND"));

        var query = _db.Questions.AsNoTracking().Where(x => x.SubjectId == subjectId && x.Status != 255);
        if (questionTypeId.HasValue) query = query.Where(x => x.QuestionTypeId == questionTypeId.Value);
        if (questionLevelId.HasValue) query = query.Where(x => x.QuestionLevelId == questionLevelId.Value);
        var total = await query.CountAsync();
        if (total == 0) return NotFound(ApiResponse<string>.Fail("NO_QUESTIONS"));
        if (total > MaxRows) return Conflict(ApiResponse<string>.Fail("EXPORT_TOO_LARGE"));

        var questions = await query.Include(x => x.Answers).OrderBy(x => x.Id).ToListAsync();
        var errors = new List<ExportIssue>();
        var rows = new List<string[]>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var question in questions)
        {
            var before = errors.Count;
            var content = question.Content.Trim();
            var answers = question.Answers.OrderBy(x => x.Id).ToList();
            void Add(string code, string message) => errors.Add(new(question.Id, code, message));

            if (question.QuestionLevelId is null or <= 0) Add("LEVEL_MISSING", "Câu hỏi chưa có mức độ");
            if (question.QuestionTypeId is < 1 or > 3) Add("TYPE_UNSUPPORTED", "Chỉ xuất được loại câu hỏi 1, 2, 3");
            if (content.Length is < 1 or > 4000) Add("CONTENT_LENGTH", "Nội dung cần 1–4000 ký tự");
            if (answers.Count is < 2 or > 20) Add("ANSWER_COUNT", "Cần 2–20 đáp án");
            if (answers.Any(x => x.Content.Contains(';'))) Add("ANSWER_SEPARATOR", "Đáp án chứa dấu chấm phẩy");
            if (answers.Any(x => x.Content.Trim().Length is < 1 or > 2000)) Add("ANSWER_LENGTH", "Đáp án cần 1–2000 ký tự");
            if (answers.Any(x => x.Content != x.Content.Trim())) Add("ANSWER_WHITESPACE", "Đáp án có khoảng trắng đầu hoặc cuối");
            if (HasInvalidXmlChar(content) || answers.Any(x => HasInvalidXmlChar(x.Content)))
                Add("INVALID_XML_CHAR", "Nội dung chứa ký tự không hợp lệ trong Excel");
            if (errors.Count == before)
            {
                var dto = new QuestionCreateDto { SubjectId = question.SubjectId, QuestionLevelId = question.QuestionLevelId,
                    QuestionTypeId = question.QuestionTypeId, Content = content, Status = 1,
                    Answers = answers.Select(x => new QuestionAnswerInputDto { Content = x.Content, IsCorrect = x.IsCorrect }).ToList() };
                try { QuestionService.Validate(dto); }
                catch (BadRequestException ex) { Add("INVALID_ANSWERS", ex.Message); }
            }
            if (!seen.Add(content)) Add("DUPLICATE_CONTENT", "Nội dung trùng với câu khác cùng môn");
            if (errors.Count > before) continue;

            var correct = answers.Select((answer, i) => (answer, Index: i + 1)).Where(x => x.answer.IsCorrect)
                .Select(x => x.Index.ToString());
            rows.Add([question.SubjectId.ToString(), question.QuestionLevelId!.Value.ToString(),
                question.QuestionTypeId.ToString(), content, string.Join(";", answers.Select(x => x.Content)),
                string.Join(",", correct)]);
        }
        if (errors.Count > 0) return Conflict(new ApiResponse<object>
        { Success = false, Message = "Có câu hỏi không xuất được theo định dạng v1", Data = new { exported = 0, errors } });

        var bytes = QuestionWorkbook.Write(rows);
        if (bytes.Length > MaxBytes) return Conflict(ApiResponse<string>.Fail("EXPORT_TOO_LARGE"));

        // Audit trước khi trả file; lỗi ghi audit thì 500 và không có file nào được trả.
        _audit.Add(User, "question.export", "Subject", subjectId.ToString(),
            $"Xuất {rows.Count} câu hỏi; questionTypeId={questionTypeId?.ToString() ?? "-"}; questionLevelId={questionLevelId?.ToString() ?? "-"}");
        await _db.SaveChangesAsync();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"dacn-questions-v1-subject-{subjectId}.xlsx");
    }
}
