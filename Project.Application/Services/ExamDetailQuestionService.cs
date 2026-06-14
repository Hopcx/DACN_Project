using Project.Application.DTOs.ExamDetailQuestionDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ExamDetailQuestionService : IExamDetailQuestionService
    {
        private readonly IExamDetailQuestionRepository _repository;

        public ExamDetailQuestionService(IExamDetailQuestionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ExamDetailQuestionResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllExamDetailQuestionsAsync();
            return items.Select(x => new ExamDetailQuestionResponseDto
            {
                Id = x.Id,
                ExamDetailId = x.ExamDetailId,
                QuestionId = x.QuestionId,
                Point = x.Point
            }).ToList();
        }

        public async Task<ExamDetailQuestionResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetExamDetailQuestionByIdAsync(id);
            if (item == null)
                return null;

            return new ExamDetailQuestionResponseDto
            {
                Id = item.Id,
                ExamDetailId = item.ExamDetailId,
                QuestionId = item.QuestionId,
                Point = item.Point
            };
        }

        public async Task<ExamDetailQuestionResponseDto?> CreateAsync(ExamDetailQuestionCreateDto dto)
        {
            var entity = new ExamDetailQuestion
            {
                ExamDetailId = dto.ExamDetailId,
                QuestionId = dto.QuestionId,
                Point = dto.Point
            };

            var created = await _repository.CreateExamDetailQuestionAsync(entity);
            if (created == null)
                return null;

            return new ExamDetailQuestionResponseDto
            {
                Id = created.Id,
                ExamDetailId = created.ExamDetailId,
                QuestionId = created.QuestionId,
                Point = created.Point
            };
        }

        public async Task<ExamDetailQuestionResponseDto?> UpdateAsync(int id, ExamDetailQuestionCreateDto dto)
        {
            var entity = new ExamDetailQuestion
            {
                ExamDetailId = dto.ExamDetailId,
                QuestionId = dto.QuestionId,
                Point = dto.Point
            };

            var updated = await _repository.UpdateExamDetailQuestionAsync(id, entity);
            if (updated == null)
                return null;

            return new ExamDetailQuestionResponseDto
            {
                Id = updated.Id,
                ExamDetailId = updated.ExamDetailId,
                QuestionId = updated.QuestionId,
                Point = updated.Point
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteExamDetailQuestionAsync(id);
            return deleted != null;
        }
    }
}
