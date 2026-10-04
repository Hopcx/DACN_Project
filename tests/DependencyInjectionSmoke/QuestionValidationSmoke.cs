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
        public Task<List<Question>> GetAllQuestionsAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId) => Task.FromResult(new List<Question>());
        public Task<Question> GetQuestionByIdAsync(int id) => Task.FromResult<Question>(null!);
        public Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId) => Task.FromResult(new List<Question>());
        public Task<Question> CreateQuestionAsync(Question question) => Task.FromResult(question);
        public Task<Question> UpdateQuestionAsync(int id, Question question) => Task.FromResult(question);
        public Task<Question> DeleteQuestionAsync(int id) => Task.FromResult<Question>(null!);
        public Task<bool> ReferencesExistAsync(int subjectId, int questionTypeId, int? questionLevelId) => Task.FromResult(true);
        public Task<List<Subject>> GetAvailableSubjectsAsync() => Task.FromResult(new List<Subject>());
        public Task<Question?> SaveWithAnswersAsync(int? id, Question question, IReadOnlyList<Answer> answers)
        {
            question.Answers = answers.ToList();
            return Task.FromResult<Question?>(question);
        }
    }
}
