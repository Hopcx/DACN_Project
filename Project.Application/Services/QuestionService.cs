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

    public async Task<List<QuestionResponseDto>> GetAllAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId,
        int? chapterId = null, IReadOnlyCollection<int>? tagIds = null)
    {
        if (new[] { subjectId, questionTypeId, questionLevelId, chapterId }.Any(x => x.HasValue && x.Value <= 0) ||
            (tagIds?.Any(x => x <= 0) ?? false))
            throw new BadRequestException("Bộ lọc phải là ID dương.");
        var items = await _repository.GetAllQuestionsAsync(textSearch, subjectId, questionTypeId, questionLevelId, chapterId, tagIds);
        return items.Select(Map).ToList();
    }

    public async Task<QuestionResponseDto?> GetByIdAsync(int id)
    {
        var item = await _repository.GetQuestionByIdAsync(id);
        return item == null ? null : Map(item);
    }

    public async Task<QuestionResponseDto?> CreateAsync(QuestionCreateDto dto)
    {
        await ValidateAllAsync(dto, null);
        var created = await _repository.SaveWithAnswersAsync(null, Entity(dto), BuildAnswers(dto), TagIdsOf(dto));
        return created == null ? null : Map(created);
    }

    public async Task<QuestionResponseDto?> UpdateAsync(int id, QuestionCreateDto dto)
    {
        await ValidateAllAsync(dto, id);
        var updated = await _repository.SaveWithAnswersAsync(id, Entity(dto), BuildAnswers(dto), TagIdsOf(dto));
        return updated == null ? null : Map(updated);
    }

    public async Task<bool> DeleteAsync(int id) => await _repository.DeleteQuestionAsync(id) != null;

    public async Task<List<QuestionSubjectDto>> GetSubjectsAsync() =>
        (await _repository.GetAvailableSubjectsAsync()).Select(x => new QuestionSubjectDto { Id = x.Id, Name = x.Name }).ToList();

    public const int MaxTagsPerQuestion = 10;

    private async Task ValidateAllAsync(QuestionCreateDto dto, int? questionId)
    {
        Validate(dto);
        if (!await _repository.ReferencesExistAsync(dto.SubjectId, dto.QuestionTypeId, dto.QuestionLevelId))
            throw new BadRequestException("Môn, loại hoặc mức độ không tồn tại hoặc đã ngừng sử dụng.");
        var problem = await _repository.MetadataProblemAsync(dto.SubjectId, dto.ChapterId, TagIdsOf(dto), questionId);
        if (problem != null) throw new BadRequestException(problem);
    }

    private static List<int> TagIdsOf(QuestionCreateDto dto) => (dto.TagIds ?? new List<int>()).Distinct().ToList();

    public static void Validate(QuestionCreateDto dto)
    {
        // To-be T2: chương, tag, lời giải
        if (dto.ChapterId is <= 0) throw new BadRequestException("Chương phải là ID dương.");
        if (dto.TagIds != null && (dto.TagIds.Any(x => x <= 0) || dto.TagIds.Distinct().Count() > MaxTagsPerQuestion))
            throw new BadRequestException($"Tag phải là ID dương, tối đa {MaxTagsPerQuestion} tag mỗi câu.");
        if (dto.Explanation != null && dto.Explanation.Trim().Length > 4000)
            throw new BadRequestException("Lời giải tối đa 4000 ký tự.");
        if (string.IsNullOrWhiteSpace(dto.Content) || dto.Content.Trim().Length > 4000)
            throw new BadRequestException("Nội dung câu hỏi phải có từ 1 đến 4000 ký tự.");
        if (dto.SubjectId <= 0 || dto.QuestionLevelId is <= 0 || !QuestionKinds.IsSupported(dto.QuestionTypeId))
            throw new BadRequestException("Môn, loại và mức độ câu hỏi phải hợp lệ; chỉ hỗ trợ loại 1–6.");
        if (dto.Status is not null and not 0 and not 1)
            throw new BadRequestException("Trạng thái chỉ nhận 0 hoặc 1.");
        if (dto.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId) { ValidateShortAnswer(dto); return; }
        if (QuestionKinds.HasItems(dto.QuestionTypeId)) { ValidateItems(dto); return; }
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

    /// <summary>To-be T17 (QT17-2, QT17-3): loại 5 = 2–10 cặp (vế trái/vế phải 1–2000 ký tự, không trùng, không phân biệt hoa/thường);
    /// loại 6 = 2–10 mệnh đề không trùng, Đúng/Sai tùy ý (có thể cùng đúng hoặc cùng sai).</summary>
    private static void ValidateItems(QuestionCreateDto dto)
    {
        var matching = dto.QuestionTypeId == QuestionKinds.Matching;
        var name = matching ? "cặp" : "mệnh đề";
        if (dto.Answers == null || dto.Answers.Count < QuestionKinds.MinItems || dto.Answers.Count > QuestionKinds.MaxItems ||
            dto.Answers.Any(x => x == null || string.IsNullOrWhiteSpace(x.Content) || x.Content.Trim().Length > 2000))
            throw new BadRequestException($"Cần {QuestionKinds.MinItems}–{QuestionKinds.MaxItems} {name}, mỗi {name} có nội dung từ 1 đến 2000 ký tự.");
        if (dto.Answers.Select(x => x.Content.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dto.Answers.Count)
            throw new BadRequestException(matching ? "Vế trái của các cặp không được trùng." : "Nội dung mệnh đề không được trùng.");
        if (!matching) return;
        if (dto.Answers.Any(x => string.IsNullOrWhiteSpace(x.MatchContent) || x.MatchContent.Trim().Length > 2000))
            throw new BadRequestException("Mỗi cặp cần vế phải từ 1 đến 2000 ký tự.");
        if (dto.Answers.Select(x => x.MatchContent!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dto.Answers.Count)
            throw new BadRequestException("Vế phải của các cặp không được trùng.");
    }

    /// <summary>To-be T16 (QT16-3): 1–10 đáp án chấp nhận (đều đúng), mỗi đáp án 1–200 ký tự, không trùng sau chuẩn hóa;
    /// sai số chỉ khi bật "so số", 0 ≤ sai số ≤ 1e9.</summary>
    private static void ValidateShortAnswer(QuestionCreateDto dto)
    {
        var max = ShortAnswerMatcher.MaxTextLength;
        if (dto.Answers == null || dto.Answers.Count < 1 || dto.Answers.Count > ShortAnswerMatcher.MaxAccepted ||
            dto.Answers.Any(x => x == null || string.IsNullOrWhiteSpace(x.Content) || x.Content.Trim().Length > max))
            throw new BadRequestException($"Câu điền ngắn cần 1–{ShortAnswerMatcher.MaxAccepted} đáp án chấp nhận, mỗi đáp án 1–{max} ký tự.");
        if (dto.Answers.Any(x => !x.IsCorrect))
            throw new BadRequestException("Mọi đáp án của câu điền ngắn đều là đáp án chấp nhận (đúng).");
        var ignore = dto.ShortAnswerIgnoreDiacritics == true;
        if (dto.Answers.Select(x => ShortAnswerMatcher.Normalize(x.Content, ignore)).Distinct().Count() != dto.Answers.Count)
            throw new BadRequestException("Đáp án chấp nhận bị trùng sau khi chuẩn hóa (hoa/thường, khoảng trắng" + (ignore ? ", dấu" : "") + ").");
        if (dto.ShortAnswerTolerance != null && dto.ShortAnswerNumeric != true)
            throw new BadRequestException("Sai số chỉ dùng khi bật so số.");
        if (dto.ShortAnswerTolerance is < 0 or > ShortAnswerMatcher.MaxTolerance || (dto.ShortAnswerTolerance is { } t && decimal.Round(t, 6) != t))
            throw new BadRequestException("Sai số phải từ 0 đến 1.000.000.000, tối đa 6 chữ số thập phân.");
    }

    private static Question Entity(QuestionCreateDto dto) => new()
    {
        Content = dto.Content.Trim(), Status = dto.Status ?? 1, SubjectId = dto.SubjectId,
        CreatedDate = DateTime.Now, CreatedAt = DateTime.Now,
        QuestionTypeId = dto.QuestionTypeId, QuestionLevelId = dto.QuestionLevelId,
        DocumentPath = dto.DocumentPath, ChapterId = dto.ChapterId,
        Explanation = string.IsNullOrWhiteSpace(dto.Explanation) ? null : dto.Explanation.Trim(),
        // To-be T16: chỉ loại 4 lưu tùy chọn so khớp; loại khác luôn null (bỏ qua giá trị client gửi)
        ShortAnswerIgnoreDiacritics = dto.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId ? dto.ShortAnswerIgnoreDiacritics == true : null,
        ShortAnswerNumeric = dto.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId ? dto.ShortAnswerNumeric == true : null,
        ShortAnswerTolerance = dto.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId && dto.ShortAnswerNumeric == true ? dto.ShortAnswerTolerance ?? 0 : null
    };

    // T17: ghép cặp lưu vế phải ở MatchContent (IsCorrect = true cho mọi cặp); loại khác MatchContent = null
    private static List<Answer> BuildAnswers(QuestionCreateDto dto) => dto.Answers.Select(x => new Answer
    {
        Content = x.Content.Trim(), Status = 1,
        IsCorrect = dto.QuestionTypeId == QuestionKinds.Matching || x.IsCorrect,
        MatchContent = dto.QuestionTypeId == QuestionKinds.Matching ? x.MatchContent?.Trim() : null
    }).ToList();

    private static QuestionResponseDto Map(Question item) => new()
    {
        Id = item.Id, Content = item.Content, CreatedDate = item.CreatedAt ?? item.CreatedDate,
        Status = item.Status, SubjectId = item.SubjectId, DocumentPath = item.DocumentPath,
        QuestionTypeId = item.QuestionTypeId, QuestionLevelId = item.QuestionLevelId,
        IsUsedInExam = item.IsUsedInExam, ChapterId = item.ChapterId, Explanation = item.Explanation,
        TagIds = item.QuestionTags.Select(x => x.TagId).OrderBy(x => x).ToList(), MediaCount = item.QuestionMedia.Count,
        ShortAnswerIgnoreDiacritics = item.ShortAnswerIgnoreDiacritics, ShortAnswerNumeric = item.ShortAnswerNumeric,
        ShortAnswerTolerance = item.ShortAnswerTolerance,
        GroupId = item.GroupId, GroupOrder = item.GroupOrder, GroupTitle = item.Group?.Title,   // To-be T18
        Answers = item.Answers.Where(x => x.Status != 255).OrderBy(x => x.Id).Select(x => new QuestionAnswerResponseDto
        { Id = x.Id, Content = x.Content, IsCorrect = x.IsCorrect, MatchContent = x.MatchContent }).ToList()
    };
}
