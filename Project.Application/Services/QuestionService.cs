using Project.Application.DTOs.QuestionDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepository _repository;

        public QuestionService(IQuestionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<QuestionResponseDto>> GetAllAsync(string? textSearch)
        {
            var items = await _repository.GetAllQuestionsAsync(textSearch);
            return items.Select(x => new QuestionResponseDto
            {
                Id = x.Id,
                Content = x.Content,
                CreatedDate = x.CreatedAt ?? default,
                Status = x.Status,
                SubjectId = x.SubjectId,
                DocumentPath = x.DocumentPath,
                QuestionTypeId = x.QuestionTypeId,
                QuestionLevelId = x.QuestionLevelId
            }).ToList();
        }

        public async Task<QuestionResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetQuestionByIdAsync(id);
            if (item == null)
                return null;

            return new QuestionResponseDto
            {
                Id = item.Id,
                Content = item.Content,
                CreatedDate = item.CreatedAt ?? default,
                Status = item.Status,
                SubjectId = item.SubjectId,
                DocumentPath = item.DocumentPath,
                QuestionTypeId = item.QuestionTypeId,
                QuestionLevelId = item.QuestionLevelId
            };
        }

        public async Task<QuestionResponseDto?> CreateAsync(QuestionCreateDto dto)
        {
            var entity = new Question
            {
                Content = dto.Content,
                Status = dto.Status,
                SubjectId = dto.SubjectId,
                DocumentPath = dto.DocumentPath,
                QuestionTypeId = dto.QuestionTypeId,
                QuestionLevelId = dto.QuestionLevelId
            };

            var created = await _repository.CreateQuestionAsync(entity);
            if (created == null)
                return null;

            return new QuestionResponseDto
            {
                Id = created.Id,
                Content = created.Content,
                CreatedDate = created.CreatedAt ?? default,
                Status = created.Status,
                SubjectId = created.SubjectId,
                DocumentPath = created.DocumentPath,
                QuestionTypeId = created.QuestionTypeId,
                QuestionLevelId = created.QuestionLevelId
            };
        }

        public async Task<QuestionResponseDto?> UpdateAsync(int id, QuestionCreateDto dto)
        {
            var entity = new Question
            {
                Content = dto.Content,
                Status = dto.Status,
                SubjectId = dto.SubjectId,
                DocumentPath = dto.DocumentPath,
                QuestionTypeId = dto.QuestionTypeId,
                QuestionLevelId = dto.QuestionLevelId
            };

            var updated = await _repository.UpdateQuestionAsync(id, entity);
            if (updated == null)
                return null;

            return new QuestionResponseDto
            {
                Id = updated.Id,
                Content = updated.Content,
                CreatedDate = updated.CreatedAt ?? default,
                Status = updated.Status,
                SubjectId = updated.SubjectId,
                DocumentPath = updated.DocumentPath,
                QuestionTypeId = updated.QuestionTypeId,
                QuestionLevelId = updated.QuestionLevelId
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteQuestionAsync(id);
            return deleted != null;
        }
    }
}
