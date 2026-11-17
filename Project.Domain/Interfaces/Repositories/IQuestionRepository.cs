using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IQuestionRepository
    {
        // To-be T2: lọc thêm theo chương và tag (câu có ít nhất một tag trong danh sách)
        Task<List<Question>> GetAllQuestionsAsync(string? textSearch, int? subjectId, int? questionTypeId, int? questionLevelId,
            int? chapterId = null, IReadOnlyCollection<int>? tagIds = null);
        Task<Question> GetQuestionByIdAsync(int id);
        Task<List<Question>> GetQuestionsBySubjectIdAsync(int subjectId);
        Task<Question> CreateQuestionAsync(Question question);
        Task<Question> UpdateQuestionAsync(int id, Question question);
        Task<Question> DeleteQuestionAsync(int id);
        // tagIds = tập tag thay thế toàn bộ (null coi như rỗng), lưu cùng transaction với câu và đáp án
        Task<Question?> SaveWithAnswersAsync(int? id, Question question, IReadOnlyList<Answer> answers,
            IReadOnlyCollection<int>? tagIds = null);
        Task<bool> ReferencesExistAsync(int subjectId, int questionTypeId, int? questionLevelId);
        /// <summary>Lỗi chương/tag của câu (null = hợp lệ): chương phải tồn tại, chưa ẩn, cùng môn; tag phải tồn tại và đang hoạt động.</summary>
        Task<string?> MetadataProblemAsync(int subjectId, int? chapterId, IReadOnlyCollection<int> tagIds, int? questionId = null);
        Task<List<Subject>> GetAvailableSubjectsAsync();
    }
}
