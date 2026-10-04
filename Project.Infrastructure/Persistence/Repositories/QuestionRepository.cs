using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;

        public QuestionRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }

        public async Task<List<Question>> GetAllQuestionsAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId)
        {
            var query = _context.Questions.Include(x => x.Answers).AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                query = query.Where(x => x.Content.Contains(textSearch.Trim()));
            }
            if (subjectId.HasValue) query = query.Where(x => x.SubjectId == subjectId.Value);
            if (questionTypeId.HasValue) query = query.Where(x => x.QuestionTypeId == questionTypeId.Value);
            if (questionLevelId.HasValue) query = query.Where(x => x.QuestionLevelId == questionLevelId.Value);

            var questions = await query.OrderBy(x => x.Id).ToListAsync();
            var ids = questions.Select(x => x.Id).ToList();
            var used = await _context.ExamDetailQuestions.Where(x => ids.Contains(x.QuestionId))
                .Select(x => x.QuestionId).Distinct().ToListAsync();
            foreach (var question in questions) question.IsUsedInExam = used.Contains(question.Id);
            return questions;
        }

        public async Task<Question> GetQuestionByIdAsync(int id)
        {
            var question = await _context.Questions.Include(x => x.Answers).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (question != null)
                question.IsUsedInExam = await _context.ExamDetailQuestions.AnyAsync(x => x.QuestionId == id);
            return question;
        }

        public async Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId)
        {
            return await _context.Questions.Where(x => x.SubjectId == subjectId).ToListAsync();
        }

        public async Task<Question> CreateQuestionAsync(Question question)
        {
            try
            {
                var created = _context.Questions.Add(question).Entity;
                await _context.SaveChangesAsync();
                return created;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Question> UpdateQuestionAsync(int id, Question question)
        {
            var existing = await _context.Questions.FindAsync(id);
            if (existing == null)
                return null;

            existing.Content = question.Content;
            existing.Status = question.Status;
            existing.SubjectId = question.SubjectId;
            existing.DocumentPath = question.DocumentPath;
            existing.QuestionTypeId = question.QuestionTypeId;
            existing.QuestionLevelId = question.QuestionLevelId;
            existing.UpdatedBy = question.UpdatedBy;
            existing.UpdatedAt = question.UpdatedAt;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Question> DeleteQuestionAsync(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var existing = await _context.Questions.FindAsync(id);
            if (existing == null)
                return null;
            if (await _context.ExamDetailQuestions.AnyAsync(x => x.QuestionId == id) ||
                await _context.AnswerSubmissions.AnyAsync(x => x.QuestionId == id))
                return null;

            existing.Status = 255;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return existing;
        }

        public async Task<Question?> SaveWithAnswersAsync(int? id, Question incoming, IReadOnlyList<Answer> answers)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            Question question;
            if (id.HasValue)
            {
                question = await _context.Questions.Include(x => x.Answers)
                    .FirstOrDefaultAsync(x => x.Id == id.Value);
                if (question == null || await _context.ExamDetailQuestions.AnyAsync(x => x.QuestionId == id.Value) ||
                    await _context.AnswerSubmissions.AnyAsync(x => x.QuestionId == id.Value) ||
                    await _context.QuestionAnswers.AnyAsync(x => x.QuestionId == id.Value))
                    return null;
                question.Content = incoming.Content;
                question.Status = incoming.Status;
                question.SubjectId = incoming.SubjectId;
                question.QuestionTypeId = incoming.QuestionTypeId;
                question.QuestionLevelId = incoming.QuestionLevelId;
                question.DocumentPath = incoming.DocumentPath;
                // Physical removal is safe only for questions never placed in an exam.
                _context.Answers.RemoveRange(question.Answers);
                await _context.SaveChangesAsync();
            }
            else
            {
                question = incoming;
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();
            }
            foreach (var answer in answers)
            {
                answer.QuestionId = question.Id;
                _context.Answers.Add(answer);
            }
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetQuestionByIdAsync(question.Id);
        }

        public async Task<bool> ReferencesExistAsync(int subjectId, int questionTypeId, int? questionLevelId) =>
            await _context.Subjects.AnyAsync(x => x.Id == subjectId && x.Status == 1) &&
            await _context.QuestionTypes.AnyAsync(x => x.Id == questionTypeId && x.Status == true) &&
            (!questionLevelId.HasValue || await _context.QuestionLevels.AnyAsync(x => x.Id == questionLevelId.Value && x.Status == true));

        public Task<List<Subject>> GetAvailableSubjectsAsync() =>
            _context.Subjects.AsNoTracking().Where(x => x.Status == 1).OrderBy(x => x.Name).ToListAsync();
    }
}
