using Project.Application.DTOs.ExamDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ExamService : IExamService
    {
        private readonly IExamRepository _repository;

        public ExamService(IExamRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ExamResponseDto>> GetAllAsync(string? textSearch, bool? isActive)
        {
            var items = await _repository.GetAllExamsAsync(textSearch, isActive);
            return items.Select(x => new ExamResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status,
                SubjectId = x.SubjectId,
                NumberOfQuestions = x.NumberOfQuestions,
                MaximmumMark = x.MaximmumMark,
                PassMark = x.PassMark,
                Duration = x.Duration
            }).ToList();
        }

        public async Task<ExamResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetExamByIdAsync(id);
            if (item == null)
                return null;

            return new ExamResponseDto
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Status = item.Status,
                SubjectId = item.SubjectId,
                NumberOfQuestions = item.NumberOfQuestions,
                MaximmumMark = item.MaximmumMark,
                PassMark = item.PassMark,
                Duration = item.Duration
            };
        }

        public async Task<ExamResponseDto?> CreateAsync(ExamCreateDto dto)
        {
            var entity = new Exam
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status ?? 0,
                SubjectId = dto.SubjectId,
                NumberOfQuestions = dto.NumberOfQuestions,
                MaximmumMark = dto.MaximmumMark,
                PassMark = dto.PassMark,
                Duration = dto.Duration
            };

            var created = await _repository.CreateExamAsync(entity);
            if (created == null)
                return null;

            return new ExamResponseDto
            {
                Id = created.Id,
                Name = created.Name,
                Description = created.Description,
                Status = created.Status,
                SubjectId = created.SubjectId,
                NumberOfQuestions = created.NumberOfQuestions,
                MaximmumMark = created.MaximmumMark,
                PassMark = created.PassMark,
                Duration = created.Duration
            };
        }

        public async Task<ExamResponseDto?> UpdateAsync(int id, ExamCreateDto dto)
        {
            var entity = new Exam
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status ?? 0,
                SubjectId = dto.SubjectId,
                NumberOfQuestions = dto.NumberOfQuestions,
                MaximmumMark = dto.MaximmumMark,
                PassMark = dto.PassMark,
                Duration = dto.Duration
            };

            var updated = await _repository.UpdateExamAsync(id, entity);
            if (updated == null)
                return null;

            return new ExamResponseDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Status = updated.Status,
                SubjectId = updated.SubjectId,
                NumberOfQuestions = updated.NumberOfQuestions,
                MaximmumMark = updated.MaximmumMark,
                PassMark = updated.PassMark,
                Duration = updated.Duration
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteExamAsync(id);
            return deleted != null;
        }
    }
}
