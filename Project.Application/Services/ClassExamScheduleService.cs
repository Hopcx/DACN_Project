using Project.Application.DTOs.ClassExamScheduleDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ClassExamScheduleService : IClassExamScheduleService
    {
        private readonly IClassExamScheduleRepository _repository;

        public ClassExamScheduleService(IClassExamScheduleRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ClassExamScheduleResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllClassExamSchedulesAsync();
            return items.Select(x => new ClassExamScheduleResponseDto
            {
                Id = x.Id,
                ClassId = x.ClassId,
                ExamScheduleId = x.ExamScheduleId
            }).ToList();
        }

        public async Task<ClassExamScheduleResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetClassExamScheduleByIdAsync(id);
            if (item == null)
                return null;

            return new ClassExamScheduleResponseDto
            {
                Id = item.Id,
                ClassId = item.ClassId,
                ExamScheduleId = item.ExamScheduleId
            };
        }

        public async Task<ClassExamScheduleResponseDto?> CreateAsync(ClassExamScheduleCreateDto dto)
        {
            var entity = new ClassExamSchedule
            {
                ClassId = dto.ClassId,
                ExamScheduleId = dto.ExamScheduleId
            };

            var created = await _repository.CreateClassExamScheduleAsync(entity);
            if (created == null)
                return null;

            return new ClassExamScheduleResponseDto
            {
                Id = created.Id,
                ClassId = created.ClassId,
                ExamScheduleId = created.ExamScheduleId
            };
        }

        public async Task<ClassExamScheduleResponseDto?> UpdateAsync(int id, ClassExamScheduleCreateDto dto)
        {
            var entity = new ClassExamSchedule
            {
                ClassId = dto.ClassId,
                ExamScheduleId = dto.ExamScheduleId
            };

            var updated = await _repository.UpdateClassExamScheduleAsync(id, entity);
            if (updated == null)
                return null;

            return new ClassExamScheduleResponseDto
            {
                Id = updated.Id,
                ClassId = updated.ClassId,
                ExamScheduleId = updated.ExamScheduleId
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteClassExamScheduleAsync(id);
            return deleted != null;
        }
    }
}
