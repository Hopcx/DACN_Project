using Project.Application.DTOs.AnswerSubmissionDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class AnswerSubmissionService : IAnswerSubmissionService
    {
        private readonly IAnswerSubmissionRepository _repository;

        public AnswerSubmissionService(IAnswerSubmissionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<AnswerSubmissionResponseDto>> GetAllAsync()
        {
            var results = await _repository.GetAllAnswerSubmissionsAsync();
            return results.Select(x => new AnswerSubmissionResponseDto
            {
                Id = x.Id,
                SubmissionId = x.SubmissionId,
                AnswerId = x.AnswerId,
                QuestionId = x.QuestionId
            }).ToList();
        }

        public async Task<AnswerSubmissionResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetAnswerSubmissionByIdAsync(id);
            if (item == null)
                return null;

            return new AnswerSubmissionResponseDto
            {
                Id = item.Id,
                SubmissionId = item.SubmissionId,
                AnswerId = item.AnswerId,
                QuestionId = item.QuestionId
            };
        }

        public async Task<AnswerSubmissionResponseDto?> CreateAsync(AnswerSubmissionCreateDto dto)
        {
            var entity = new AnswerSubmission
            {
                SubmissionId = dto.SubmissionId,
                AnswerId = dto.AnswerId,
                QuestionId = dto.QuestionId
            };

            var created = await _repository.CreateAnswerSubmissionAsync(entity);
            if (created == null)
                return null;

            return new AnswerSubmissionResponseDto
            {
                Id = created.Id,
                SubmissionId = created.SubmissionId,
                AnswerId = created.AnswerId,
                QuestionId = created.QuestionId
            };
        }

        public async Task<AnswerSubmissionResponseDto?> UpdateAsync(int id, AnswerSubmissionCreateDto dto)
        {
            var entity = new AnswerSubmission
            {
                SubmissionId = dto.SubmissionId,
                AnswerId = dto.AnswerId,
                QuestionId = dto.QuestionId
            };

            var updated = await _repository.UpdateAnswerSubmissionAsync(id, entity);
            if (updated == null)
                return null;

            return new AnswerSubmissionResponseDto
            {
                Id = updated.Id,
                SubmissionId = updated.SubmissionId,
                AnswerId = updated.AnswerId,
                QuestionId = updated.QuestionId
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteAnswerSubmissionAsync(id);
            return deleted != null;
        }
    }
}
