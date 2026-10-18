using Project.Application.DTOs.AttemptDTO;
using Project.Domain.Entities;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

// Pure preparation for the attempt store: no DB writes, eligibility, timing or grading.
public static class AttemptSnapshotFactory
{
    public static AttemptSnapshot Create(Exam exam, ExamDetail variant)
    {
        if (exam.Id <= 0 || variant.Id <= 0 || variant.ExamId != exam.Id || exam.Status != 1 || variant.Status != 1)
            throw new ArgumentException("Bài thi và mã đề phải được công khai và thuộc cùng bài thi.");
        var links = variant.ExamDetailQuestions.OrderBy(x => x.Id).ToArray();
        if (exam.NumberOfQuestions <= 0 || links.Length != exam.NumberOfQuestions ||
            links.Select(x => x.QuestionId).Distinct().Count() != links.Length)
            throw new ArgumentException("Mã đề phải có đúng số câu, không trùng câu hỏi.");
        var questions = new List<AttemptQuestionSnapshot>();
        foreach (var link in links)
        {
            var question = link.Question;
            if (link.ExamDetailId != variant.Id || question == null || question.Id <= 0 ||
                question.Id != link.QuestionId || question.SubjectId != exam.SubjectId ||
                question.Status != 1 || question.QuestionTypeId is < 1 or > 3 ||
                string.IsNullOrWhiteSpace(question.Content) || !double.IsFinite(link.Point) || link.Point < 0)
                throw new ArgumentException("Câu hỏi của mã đề không hợp lệ hoặc không được hỗ trợ.");
            var answers = question.Answers.Where(x => x.Status == 1).OrderBy(x => x.Id).ToArray();
            var correct = answers.Count(x => x.IsCorrect);
            if (answers.Length < 2 || answers.Select(x => x.Id).Distinct().Count() != answers.Length ||
                answers.Any(x => x.Id <= 0 || x.QuestionId != question.Id || string.IsNullOrWhiteSpace(x.Content)) ||
                (question.QuestionTypeId == 1 && answers.Length != 2) ||
                (question.QuestionTypeId is 1 or 2 && correct != 1) || correct == 0)
                throw new ArgumentException("Tập đáp án của câu hỏi không hợp lệ.");
            questions.Add(new AttemptQuestionSnapshot(Guid.NewGuid(), question.Id, question.QuestionTypeId,
                question.Content, link.Point, Array.AsReadOnly(answers.Select(answer =>
                    new AttemptOptionSnapshot(Guid.NewGuid(), answer.Id, answer.Content, answer.IsCorrect)).ToArray())));
        }
        return new AttemptSnapshot(1, exam.Id, variant.Id, exam.Name, variant.Code, questions.AsReadOnly());
    }

    public static IReadOnlyList<AttemptQuestionDto> ForStudent(AttemptSnapshot snapshot) =>
        Array.AsReadOnly(snapshot.Questions.Select(question => new AttemptQuestionDto(
            question.Id, question.QuestionTypeId, question.Content,
            Array.AsReadOnly(question.Options.Select(option => new AttemptOptionDto(option.Id, option.Content)).ToArray()))).ToArray());

    // Full replacement: omitted questions become unanswered. Validate all input before
    // returning a canonical, detached draft; the future store owns revision and transaction.
    public static IReadOnlyList<AttemptAnswerDto> ValidateDraft(AttemptSnapshot snapshot,
        IReadOnlyList<AttemptAnswerDto>? answers)
    {
        if (answers == null || answers.Count > snapshot.Questions.Count ||
            answers.Any(x => x == null || x.SelectedAnswerIds == null) ||
            answers.Select(x => x.QuestionId).Distinct().Count() != answers.Count)
            throw new ArgumentException("Danh sách câu trả lời thiếu hoặc chứa câu hỏi trùng.");
        var questions = snapshot.Questions.ToDictionary(x => x.Id);
        foreach (var answer in answers)
        {
            if (!questions.TryGetValue(answer.QuestionId, out var question) ||
                answer.SelectedAnswerIds.Distinct().Count() != answer.SelectedAnswerIds.Count ||
                answer.SelectedAnswerIds.Any(id => !question.Options.Any(option => option.Id == id)) ||
                (question.QuestionTypeId is 1 or 2 && answer.SelectedAnswerIds.Count > 1))
                throw new ArgumentException("Câu hỏi hoặc đáp án không thuộc lượt thi, bị trùng hoặc chọn quá số lượng.");
        }
        var incoming = answers.ToDictionary(x => x.QuestionId);
        return Array.AsReadOnly(snapshot.Questions.Select(question => new AttemptAnswerDto(question.Id,
            Array.AsReadOnly(question.Options.Where(option => incoming.TryGetValue(question.Id, out var answer) &&
                answer.SelectedAnswerIds.Contains(option.Id)).Select(option => option.Id).ToArray()))).ToArray());
    }
}
