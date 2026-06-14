using Project.Application.DTOs.ClassDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ClassService : IClassService
    {
        private readonly IClassRepository _repository;

        public ClassService(IClassRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ClassResponseDto>> GetAllAsync(string? textSearch)
        {
            var items = await _repository.GetAllClassesAsync(textSearch);
            return items.Select(x => new ClassResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                ClassCode = x.ClassCode,
                Description = x.Description,
                Capacity = x.Capacity,
                TeacherId = x.TeacherId,
                SubjectId = x.SubjectId,
                Status = x.Status
            }).ToList();
        }

        public async Task<ClassResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetClassByIdAsync(id);
            if (item == null)
                return null;

            return new ClassResponseDto
            {
                Id = item.Id,
                Name = item.Name,
                ClassCode = item.ClassCode,
                Description = item.Description,
                Capacity = item.Capacity,
                TeacherId = item.TeacherId,
                SubjectId = item.SubjectId,
                Status = item.Status
            };
        }

        public async Task<ClassResponseDto?> CreateAsync(ClassCreateDto dto)
        {
            var entity = new Class
            {
                Name = dto.Name,
                ClassCode = dto.ClassCode,
                Description = dto.Description,
                Capacity = dto.Capacity,
                TeacherId = dto.TeacherId,
                SubjectId = dto.SubjectId,
                Status = dto.Status
            };

            var created = await _repository.CreateClassAsync(entity);
            if (created == null)
                return null;

            return new ClassResponseDto
            {
                Id = created.Id,
                Name = created.Name,
                ClassCode = created.ClassCode,
                Description = created.Description,
                Capacity = created.Capacity,
                TeacherId = created.TeacherId,
                SubjectId = created.SubjectId,
                Status = created.Status
            };
        }

        public async Task<ClassResponseDto?> UpdateAsync(int id, ClassCreateDto dto)
        {
            var entity = new Class
            {
                Name = dto.Name,
                ClassCode = dto.ClassCode,
                Description = dto.Description,
                Capacity = dto.Capacity,
                TeacherId = dto.TeacherId,
                SubjectId = dto.SubjectId,
                Status = dto.Status
            };

            var updated = await _repository.UpdateClassAsync(id, entity);
            if (updated == null)
                return null;

            return new ClassResponseDto
            {
                Id = updated.Id,
                Name = updated.Name,
                ClassCode = updated.ClassCode,
                Description = updated.Description,
                Capacity = updated.Capacity,
                TeacherId = updated.TeacherId,
                SubjectId = updated.SubjectId,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteClassAsync(id);
            return deleted != null;
        }
    }
}
