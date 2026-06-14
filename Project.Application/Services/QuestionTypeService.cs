using Project.Application.DTOs.QuestionTypeDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class QuestionTypeService : IQuestionTypeService
    {
        private readonly IQuestionTypeRepository _repository;

        public QuestionTypeService(IQuestionTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<QuestionTypeResponseDto>> GetAllQuestionTypesAsync()
        {
            var types = await _repository.GetAllQuestionTypesAsync();
            return types.Select(x => new QuestionTypeResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status
            }).ToList();
        }

        public async Task<QuestionTypeResponseDto?> GetQuestionTypeByIdAsync(int id)
        {
            var type = await _repository.GetQuestionTypeByIdAsync(id);
            if (type == null)
                return null;

            return new QuestionTypeResponseDto
            {
                Id = type.Id,
                Name = type.Name,
                Description = type.Description,
                Status = type.Status
            };
        }

        public async Task<QuestionTypeResponseDto?> CreateQuestionTypeAsync(QuestionTypeCreateDto dto)
        {
            var type = new QuestionType
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var created = await _repository.CreateQuestionTypeAsync(type);
            if (created == null)
                return null;

            return new QuestionTypeResponseDto
            {
                Id = created.Id,
                Name = created.Name,
                Description = created.Description,
                Status = created.Status
            };
        }

        public async Task<QuestionTypeResponseDto?> UpdateQuestionTypeAsync(int id, QuestionTypeCreateDto dto)
        {
            var type = new QuestionType
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var updated = await _repository.UpdateQuestionTypeAsync(id, type);
            if (updated == null)
                return null;

            return new QuestionTypeResponseDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteQuestionTypeAsync(int id)
        {
            var deleted = await _repository.DeleteQuestionTypeAsync(id);
            return deleted != null;
        }
    }
}
