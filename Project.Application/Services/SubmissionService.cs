using Project.Application.DTOs.SubmissionDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ISubmissionRepository _repository;

        public SubmissionService(ISubmissionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SubmissionResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllSubmissionsAsync();
            return items.Select(x => new SubmissionResponseDto
            {
                Id = x.Id,
                UserId = x.UserId,
                ExamDetailId = x.ExamDetailId,
                ExamScheduleId = x.ExamScheduleId,
                SubmitTime = x.SubmitTime,
                TimeTaken = x.TimeTaken,
                TotalMark = x.TotalMark,
                IsPassed = x.IsPassed,
                UnAnswered = x.UnAnswered,
                Answered = x.Answered,
                Note = x.Note,
                Type = x.Type,
                Status = x.Status
            }).ToList();
        }

        public async Task<SubmissionResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetSubmissionByIdAsync(id);
            if (item == null)
                return null;

            return new SubmissionResponseDto
            {
                Id = item.Id,
                UserId = item.UserId,
                ExamDetailId = item.ExamDetailId,
                ExamScheduleId = item.ExamScheduleId,
                SubmitTime = item.SubmitTime,
                TimeTaken = item.TimeTaken,
                TotalMark = item.TotalMark,
                IsPassed = item.IsPassed,
                UnAnswered = item.UnAnswered,
                Answered = item.Answered,
                Note = item.Note,
                Type = item.Type,
                Status = item.Status
            };
        }

        public async Task<SubmissionResponseDto?> CreateAsync(SubmissionCreateDto dto)
        {
            var entity = new Submission
            {
                UserId = dto.UserId,
                ExamDetailId = dto.ExamDetailId,
                ExamScheduleId = dto.ExamScheduleId,
                SubmitTime = dto.SubmitTime,
                TimeTaken = dto.TimeTaken,
                TotalMark = dto.TotalMark,
                IsPassed = dto.IsPassed,
                UnAnswered = dto.UnAnswered,
                Answered = dto.Answered,
                Note = dto.Note,
                Type = dto.Type,
                Status = dto.Status
            };

            var created = await _repository.CreateSubmissionAsync(entity);
            if (created == null)
                return null;

            return new SubmissionResponseDto
            {
                Id = created.Id,
                UserId = created.UserId,
                ExamDetailId = created.ExamDetailId,
                ExamScheduleId = created.ExamScheduleId,
                SubmitTime = created.SubmitTime,
                TimeTaken = created.TimeTaken,
                TotalMark = created.TotalMark,
                IsPassed = created.IsPassed,
                UnAnswered = created.UnAnswered,
                Answered = created.Answered,
                Note = created.Note,
                Type = created.Type,
                Status = created.Status
            };
        }

        public async Task<SubmissionResponseDto?> UpdateAsync(int id, SubmissionCreateDto dto)
        {
            var entity = new Submission
            {
                UserId = dto.UserId,
                ExamDetailId = dto.ExamDetailId,
                ExamScheduleId = dto.ExamScheduleId,
                SubmitTime = dto.SubmitTime,
                TimeTaken = dto.TimeTaken,
                TotalMark = dto.TotalMark,
                IsPassed = dto.IsPassed,
                UnAnswered = dto.UnAnswered,
                Answered = dto.Answered,
                Note = dto.Note,
                Type = dto.Type,
                Status = dto.Status
            };

            var updated = await _repository.UpdateSubmissionAsync(id, entity);
            if (updated == null)
                return null;

            return new SubmissionResponseDto
            {
                Id = updated.Id,
                UserId = updated.UserId,
                ExamDetailId = updated.ExamDetailId,
                ExamScheduleId = updated.ExamScheduleId,
                SubmitTime = updated.SubmitTime,
                TimeTaken = updated.TimeTaken,
                TotalMark = updated.TotalMark,
                IsPassed = updated.IsPassed,
                UnAnswered = updated.UnAnswered,
                Answered = updated.Answered,
                Note = updated.Note,
                Type = updated.Type,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteSubmissionAsync(id);
            return deleted != null;
        }
    }
}
