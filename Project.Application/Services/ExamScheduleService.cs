using Project.Application.DTOs.ExamScheduleDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ExamScheduleService : IExamScheduleService
    {
        private readonly IExamScheduleRepository _repository;

        public ExamScheduleService(IExamScheduleRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ExamScheduleResponseDto>> GetAllExamSchedulesAsync()
        {
            var schedules = await _repository.GetAllExamSchedulesAsync();
            return schedules.Select(x => new ExamScheduleResponseDto
            {
                Id = x.Id,
                SubjectId = x.SubjectId,
                //LecturerId = x.LecturerId,
                ExamId = x.ExamId,
                RoomId = x.RoomId,
                Title = x.Title,
                Description = x.Description,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Status = x.Status
            }).ToList();
        }

        public async Task<ExamScheduleResponseDto?> GetExamScheduleByIdAsync(int id)
        {
            var schedule = await _repository.GetExamScheduleByIdAsync(id);
            if (schedule == null)
                return null;

            return new ExamScheduleResponseDto
            {
                Id = schedule.Id,
                SubjectId = schedule.SubjectId,
                //LecturerId = schedule.LecturerId,
                RoomId = schedule.RoomId,
                Description = schedule.Description,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                Status = schedule.Status,
                ExamId= schedule.ExamId,
                Title = schedule.Title,
            };
        }

        public async Task<ExamScheduleResponseDto?> CreateExamScheduleAsync(ExamScheduleCreateDto dto)
        {
            var schedule = new ExamSchedule
            {
                SubjectId = dto.SubjectId,
                ExamId = dto.ExamId,
                RoomId = dto.RoomId,
                Description = dto.Description,
                Title = dto.Title,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = dto.Status
            };

            var created = await _repository.CreateExamScheduleAsync(schedule);
            if (created == null)
                return null;

            return new ExamScheduleResponseDto
            {
                Id = created.Id,
                SubjectId = created.SubjectId,
                ExamId = created.ExamId,
                RoomId = created.RoomId,
                Description = created.Description,
                Title = created.Title,
                StartTime = created.StartTime,
                EndTime = created.EndTime,
                Status = created.Status
            };
        }

        public async Task<ExamScheduleResponseDto?> UpdateExamScheduleAsync(int id, ExamScheduleCreateDto dto)
        {
            var schedule = new ExamSchedule
            {
                SubjectId = dto.SubjectId,
                ExamId = dto.ExamId,
                RoomId = dto.RoomId,
                Description = dto.Description,
                Title = dto.Title,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = dto.Status
            };

            var updated = await _repository.UpdateExamScheduleAsync(id, schedule);
            if (updated == null)
                return null;

            return new ExamScheduleResponseDto
            {
                Id = updated.Id,
                SubjectId = updated.SubjectId,
                ExamId = updated.ExamId,
                RoomId = updated.RoomId,
                Description = updated.Description,
                Title = updated.Title,
                StartTime = updated.StartTime,
                EndTime = updated.EndTime,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteExamScheduleAsync(int id)
        {
            var deleted = await _repository.DeleteExamScheduleAsync(id);
            return deleted != null;
        }
    }
}
