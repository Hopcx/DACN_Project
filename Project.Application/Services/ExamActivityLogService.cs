using Project.Application.DTOs.ExamActivityLogDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ExamActivityLogService : IExamActivityLogService
    {
        private readonly IExamActivityLogRepository _repository;

        public ExamActivityLogService(IExamActivityLogRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ExamActivityLogResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllExamActivityLogsAsync();
            return items.Select(x => new ExamActivityLogResponseDto
            {
                Id = x.Id,
                ExamId = x.ExamId,
                ExamDetailId = x.ExamDetailId,
                ExamScheduleId = x.ExamScheduleId,
                UserId = x.UserId,
                ActionTime = x.ActionTime,
                ActionType = x.ActionType
            }).ToList();
        }

        public async Task<ExamActivityLogResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetExamActivityLogByIdAsync(id);
            if (item == null)
                return null;

            return new ExamActivityLogResponseDto
            {
                Id = item.Id,
                ExamId = item.ExamId,
                ExamDetailId = item.ExamDetailId,
                ExamScheduleId = item.ExamScheduleId,
                UserId = item.UserId,
                ActionTime = item.ActionTime,
                ActionType = item.ActionType
            };
        }

        public async Task<ExamActivityLogResponseDto?> CreateAsync(ExamActivityLogCreateDto dto)
        {
            var entity = new ExamActivityLog
            {
                ExamId = dto.ExamId,
                ExamDetailId = dto.ExamDetailId,
                ExamScheduleId = dto.ExamScheduleId,
                UserId = dto.UserId,
                ActionTime = dto.ActionTime,
                ActionType = dto.ActionType
            };

            var created = await _repository.CreateExamActivityLogAsync(entity);
            if (created == null)
                return null;

            return new ExamActivityLogResponseDto
            {
                Id = created.Id,
                ExamId = created.ExamId,
                ExamDetailId = created.ExamDetailId,
                ExamScheduleId = created.ExamScheduleId,
                UserId = created.UserId,
                ActionTime = created.ActionTime,
                ActionType = created.ActionType
            };
        }

        public async Task<ExamActivityLogResponseDto?> UpdateAsync(int id, ExamActivityLogCreateDto dto)
        {
            var entity = new ExamActivityLog
            {
                ExamId = dto.ExamId,
                ExamDetailId = dto.ExamDetailId,
                ExamScheduleId = dto.ExamScheduleId,
                UserId = dto.UserId,
                ActionTime = dto.ActionTime,
                ActionType = dto.ActionType
            };

            var updated = await _repository.UpdateExamActivityLogAsync(id, entity);
            if (updated == null)
                return null;

            return new ExamActivityLogResponseDto
            {
                Id = updated.Id,
                ExamId = updated.ExamId,
                ExamDetailId = updated.ExamDetailId,
                ExamScheduleId = updated.ExamScheduleId,
                UserId = updated.UserId,
                ActionTime = updated.ActionTime,
                ActionType = updated.ActionType
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteExamActivityLogAsync(id);
            return deleted != null;
        }
    }
}
