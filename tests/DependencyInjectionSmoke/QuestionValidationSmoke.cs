using Project.Application.DTOs.QuestionDTO;
using Project.Application.Exceptions;
using Project.Application.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;

internal static class QuestionValidationSmoke
{
    public static async Task Run()
    {
        var repository = new FakeRepository();
        var service = new QuestionService(repository);
        foreach (var type in new[] { 1, 2, 3 })
        {
            var dto = Valid(type);
            if (await service.CreateAsync(dto) == null) throw new Exception($"Valid type {type} rejected.");
        }
        foreach (var change in new Action<QuestionCreateDto>[]
        {
            x => x.Answers.Clear(),
            x => x.Answers[1].Content = x.Answers[0].Content,
            x => x.Answers[0].IsCorrect = false,
            x => x.QuestionTypeId = 4,
            x => x.SubjectId = 0,
        })
        {
            var dto = Valid(2);
            change(dto);
            try { await service.CreateAsync(dto); throw new Exception("Invalid answer set accepted."); }
            catch (BadRequestException) { }
        }
        // T19 (nợ L1): chương bị ẩn giữa lần kiểm sớm và lúc lưu → repository báo trong transaction → service trả 400 cùng message
        repository.SaveProblem = "Chương không tồn tại hoặc đã bị ẩn.";
        foreach (var save in new Func<Task>[] { () => service.CreateAsync(Valid(2)), () => service.UpdateAsync(7, Valid(2)) })
        {
            try { await save(); throw new Exception("In-transaction metadata problem not reported."); }
            catch (BadRequestException ex) when (ex.Message == repository.SaveProblem) { }
        }
        repository.SaveProblem = null;
        // T19 (nợ L2): trạng thái cũ đi theo kết quả (không trả cho client), để controller phân loại audit restore/update
        repository.PreviousStatus = 255;
        var restored = await service.UpdateAsync(7, Valid(2));
        if (restored?.PreviousStatus != 255 || restored.Status != 1) throw new Exception("PreviousStatus not mapped.");
        if (System.Text.Json.JsonSerializer.Serialize(restored).Contains("PreviousStatus")) throw new Exception("PreviousStatus leaked to JSON.");
        // T19 (nợ L7): phân trang kiểm page/pageSize
        foreach (var (page, size) in new[] { (0, 20), (1, 0), (1, 101), (21474838, 100), (int.MaxValue, 100) })
        {
            try { await service.GetPageAsync(null, null, null, null, null, null, page, size); throw new Exception($"Bad page {page}/{size} accepted."); }
            catch (BadRequestException) { }
        }
        if ((await service.GetPageAsync(null, null, null, null, null, null, 2, 100)).Page != 2) throw new Exception("Valid page rejected.");
        Console.WriteLine("QUESTION_SAVE_RECHECK_PREVIOUS_STATUS_PAGING=PASS (isolated; no SQL/API)");
        Console.WriteLine("Question validation smoke passed: types 1–3 and five invalid cases.");
    }

    private static QuestionCreateDto Valid(int type) => new()
    {
        Content = "Nội dung", SubjectId = 1, QuestionTypeId = type,
        Answers = type == 1
            ? new() { new() { Content = "Đúng", IsCorrect = true }, new() { Content = "Sai" } }
            : new() { new() { Content = "A", IsCorrect = true }, new() { Content = "B" } }
    };

    private sealed class FakeRepository : IQuestionRepository
    {
        public Task<List<Question>> GetAllQuestionsAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId,
            int? chapterId = null, IReadOnlyCollection<int>? tagIds = null) => Task.FromResult(new List<Question>());
        public Task<string?> MetadataProblemAsync(int subjectId, int? chapterId, IReadOnlyCollection<int> tagIds, int? questionId = null) => Task.FromResult<string?>(null);
        public Task<(List<Question> Items, int TotalCount)> GetQuestionsPageAsync(string? textSearch, int? subjectId, int? questionTypeId,
            int? questionLevelId, int? chapterId, IReadOnlyCollection<int>? tagIds, int page, int pageSize) => Task.FromResult((new List<Question>(), 0));
        public Task<Question> GetQuestionByIdAsync(int id) => Task.FromResult<Question>(null!);
        public Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId) => Task.FromResult(new List<Question>());
        public Task<Question> CreateQuestionAsync(Question question) => Task.FromResult(question);
        public Task<Question> UpdateQuestionAsync(int id, Question question) => Task.FromResult(question);
        public Task<Question> DeleteQuestionAsync(int id) => Task.FromResult<Question>(null!);
        public Task<bool> ReferencesExistAsync(int subjectId, int questionTypeId, int? questionLevelId) => Task.FromResult(true);
        public Task<List<Subject>> GetAvailableSubjectsAsync() => Task.FromResult(new List<Subject>());
        public string? SaveProblem { get; set; }
        public byte? PreviousStatus { get; set; }
        public Task<Question?> SaveWithAnswersAsync(int? id, Question question, IReadOnlyList<Answer> answers,
            IReadOnlyCollection<int>? tagIds = null)
        {
            if (SaveProblem != null) throw new QuestionMetadataException(SaveProblem);
            question.Answers = answers.ToList();
            question.PreviousStatus = id.HasValue ? PreviousStatus : null;
            return Task.FromResult<Question?>(question);
        }
    }
}
