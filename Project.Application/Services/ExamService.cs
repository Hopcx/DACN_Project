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
                NumberOfRepeat = x.NumberOfRepeat,
                AllowViewResult = x.AllowViewResult,
                ScoreMethodId = x.ScoreMethodId,
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
                NumberOfRepeat = item.NumberOfRepeat,
                AllowViewResult = item.AllowViewResult,
                ScoreMethodId = item.ScoreMethodId,
                MaximmumMark = item.MaximmumMark,
                PassMark = item.PassMark,
                Duration = item.Duration
            };
        }

        public async Task<ExamResponseDto?> CreateAsync(ExamCreateDto dto)
        {
            Validate(dto);
            var entity = new Exam
            {
                Name = dto.Name.Trim(),
                Description = dto.Description,
                Status = dto.Status ?? 2,
                SubjectId = dto.SubjectId,
                NumberOfQuestions = dto.NumberOfQuestions,
                NumberOfRepeat = dto.NumberOfRepeat,
                AllowViewResult = dto.AllowViewResult,
                ScoreMethodId = dto.ScoreMethodId,
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
                NumberOfRepeat = created.NumberOfRepeat,
                AllowViewResult = created.AllowViewResult,
                ScoreMethodId = created.ScoreMethodId,
                MaximmumMark = created.MaximmumMark,
                PassMark = created.PassMark,
                Duration = created.Duration
            };
        }

        public async Task<ExamResponseDto?> UpdateAsync(int id, ExamCreateDto dto)
        {
            Validate(dto);
            var entity = new Exam
            {
                Name = dto.Name.Trim(),
                Description = dto.Description,
                Status = dto.Status ?? 2,
                SubjectId = dto.SubjectId,
                NumberOfQuestions = dto.NumberOfQuestions,
                NumberOfRepeat = dto.NumberOfRepeat,
                AllowViewResult = dto.AllowViewResult,
                ScoreMethodId = dto.ScoreMethodId,
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
                NumberOfRepeat = updated.NumberOfRepeat,
                AllowViewResult = updated.AllowViewResult,
                ScoreMethodId = updated.ScoreMethodId,
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

        private static void Validate(ExamCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.SubjectId <= 0 ||
                dto.NumberOfQuestions <= 0 || dto.NumberOfRepeat < 1 || dto.Duration <= 0 ||
                !double.IsFinite(dto.MaximmumMark) || dto.MaximmumMark <= 0 ||
                !double.IsFinite(dto.PassMark) || dto.PassMark <= 0 || dto.PassMark > dto.MaximmumMark ||
                dto.ScoreMethodId < 1 || (dto.Status is not null and not 1 and not 2))
                throw new ArgumentException("Cấu hình bài thi không hợp lệ.");
        }
    }
}
