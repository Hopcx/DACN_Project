using Project.Application.DTOs.QuestionDTO;
using Project.Application.Exceptions;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;

namespace Project.Application.Services;

public class QuestionService : IQuestionService
{
    private readonly IQuestionRepository _repository;
    public QuestionService(IQuestionRepository repository) => _repository = repository;

    public async Task<List<QuestionResponseDto>> GetAllAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId)
    {
        if (new[] { subjectId, questionTypeId, questionLevelId }.Any(x => x.HasValue && x.Value <= 0))
            throw new BadRequestException("Bộ lọc phải là ID dương.");
        var items = await _repository.GetAllQuestionsAsync(textSearch, subjectId, questionTypeId, questionLevelId);
        return items.Select(Map).ToList();
    }

    public async Task<QuestionResponseDto?> GetByIdAsync(int id)
    {
        var item = await _repository.GetQuestionByIdAsync(id);
        return item == null ? null : Map(item);
    }

    public async Task<QuestionResponseDto?> CreateAsync(QuestionCreateDto dto)
    {
        Validate(dto);
        if (!await _repository.ReferencesExistAsync(dto.SubjectId, dto.QuestionTypeId, dto.QuestionLevelId))
            throw new BadRequestException("Môn, loại hoặc mức độ không tồn tại hoặc đã ngừng sử dụng.");
        var created = await _repository.SaveWithAnswersAsync(null, Entity(dto), BuildAnswers(dto));
        return created == null ? null : Map(created);
    }

    public async Task<QuestionResponseDto?> UpdateAsync(int id, QuestionCreateDto dto)
    {
        Validate(dto);
        if (!await _repository.ReferencesExistAsync(dto.SubjectId, dto.QuestionTypeId, dto.QuestionLevelId))
            throw new BadRequestException("Môn, loại hoặc mức độ không tồn tại hoặc đã ngừng sử dụng.");
        var updated = await _repository.SaveWithAnswersAsync(id, Entity(dto), BuildAnswers(dto));
        return updated == null ? null : Map(updated);
    }

    public async Task<bool> DeleteAsync(int id) => await _repository.DeleteQuestionAsync(id) != null;

    public async Task<List<QuestionSubjectDto>> GetSubjectsAsync() =>
        (await _repository.GetAvailableSubjectsAsync()).Select(x => new QuestionSubjectDto { Id = x.Id, Name = x.Name }).ToList();

    private static void Validate(QuestionCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content) || dto.Content.Trim().Length > 4000)
            throw new BadRequestException("Nội dung câu hỏi phải có từ 1 đến 4000 ký tự.");
        if (dto.SubjectId <= 0 || dto.QuestionLevelId is <= 0 || dto.QuestionTypeId is < 1 or > 3)
            throw new BadRequestException("Môn, loại và mức độ câu hỏi phải hợp lệ; chỉ hỗ trợ loại 1, 2, 3.");
        if (dto.Status is not null and not 0 and not 1)
            throw new BadRequestException("Trạng thái chỉ nhận 0 hoặc 1.");
        if (dto.Answers == null || dto.Answers.Count < 2 || dto.Answers.Count > 20 ||
            dto.Answers.Any(x => string.IsNullOrWhiteSpace(x.Content) || x.Content.Trim().Length > 2000))
            throw new BadRequestException("Cần 2–20 đáp án, mỗi đáp án có nội dung từ 1 đến 2000 ký tự.");
        if (dto.Answers.Select(x => x.Content.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dto.Answers.Count)
            throw new BadRequestException("Nội dung đáp án không được trùng.");
        var correct = dto.Answers.Count(x => x.IsCorrect);
        if (dto.QuestionTypeId == 1 && (dto.Answers.Count != 2 || correct != 1 ||
            !dto.Answers.Select(x => x.Content.Trim().ToLowerInvariant()).OrderBy(x => x)
                .SequenceEqual(new[] { "sai", "đúng" }.OrderBy(x => x))))
            throw new BadRequestException("Đúng/Sai cần đúng hai đáp án Đúng và Sai, một đáp án đúng.");
        if (dto.QuestionTypeId == 2 && correct != 1)
            throw new BadRequestException("Câu chọn một đáp án phải có đúng một đáp án đúng.");
        if (dto.QuestionTypeId == 3 && correct < 1)
            throw new BadRequestException("Câu chọn nhiều đáp án cần ít nhất một đáp án đúng.");
    }

    private static Question Entity(QuestionCreateDto dto) => new()
    {
        Content = dto.Content.Trim(), Status = dto.Status ?? 1, SubjectId = dto.SubjectId,
        CreatedDate = DateTime.Now, CreatedAt = DateTime.Now,
        QuestionTypeId = dto.QuestionTypeId, QuestionLevelId = dto.QuestionLevelId,
        DocumentPath = dto.DocumentPath
    };

    private static List<Answer> BuildAnswers(QuestionCreateDto dto) => dto.Answers.Select(x => new Answer
    {
        Content = x.Content.Trim(), IsCorrect = x.IsCorrect, Status = 1
    }).ToList();

    private static QuestionResponseDto Map(Question item) => new()
    {
        Id = item.Id, Content = item.Content, CreatedDate = item.CreatedAt ?? item.CreatedDate,
        Status = item.Status, SubjectId = item.SubjectId, DocumentPath = item.DocumentPath,
        QuestionTypeId = item.QuestionTypeId, QuestionLevelId = item.QuestionLevelId,
        IsUsedInExam = item.IsUsedInExam,
        Answers = item.Answers.Where(x => x.Status != 255).OrderBy(x => x.Id).Select(x => new QuestionAnswerResponseDto
        { Id = x.Id, Content = x.Content, IsCorrect = x.IsCorrect }).ToList()
    };
}
