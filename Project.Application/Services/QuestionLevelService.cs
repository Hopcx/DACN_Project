using Project.Application.DTOs.QuestionLevelDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class QuestionLevelService : IQuestionLevelService
    {
        private readonly IQuestionLevelRepository _repository;

        public QuestionLevelService(IQuestionLevelRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<QuestionLevelResponseDto>> GetAllQuestionLevelsAsync(string? textSearch)
        {
            var levels = await _repository.GetAllQuestionLevelsAsync(textSearch);
            return levels.Select(x => new QuestionLevelResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status
            }).ToList();
        }

        public async Task<QuestionLevelResponseDto?> GetQuestionLevelByIdAsync(int id)
        {
            var level = await _repository.GetQuestionLevelByIdAsync(id);
            if (level == null)
                return null;

            return new QuestionLevelResponseDto
            {
                Id = level.Id,
                Name = level.Name,
                Description = level.Description,
                Status = level.Status
            };
        }

        public async Task<QuestionLevelResponseDto?> CreateQuestionLevelAsync(QuestionLevelCreateDto dto)
        {
            var level = new QuestionLevel
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var created = await _repository.CreateQuestionLevelAsync(level);
            if (created == null)
                return null;

            return new QuestionLevelResponseDto
            {
                Id = created.Id,
                Name = created.Name,
                Description = created.Description,
                Status = created.Status
            };
        }

        public async Task<QuestionLevelResponseDto?> UpdateQuestionLevelAsync(int id, QuestionLevelCreateDto dto)
        {
            var level = new QuestionLevel
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var updated = await _repository.UpdateQuestionLevelAsync(id, level);
            if (updated == null)
                return null;

            return new QuestionLevelResponseDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteQuestionLevelAsync(int id)
        {
            var deleted = await _repository.DeleteQuestionLevelAsync(id);
            return deleted != null;
        }
    }
}
