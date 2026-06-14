using Project.Application.DTOs.ClassUserDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ClassUserService : IClassUserService
    {
        private readonly IClassUserRepository _repository;

        public ClassUserService(IClassUserRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ClassUserResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllClassUsersAsync();
            return items.Select(x => new ClassUserResponseDto
            {
                Id = x.Id,
                ClassId = x.ClassId,
                UserId = x.UserId,
                Status = x.Status
            }).ToList();
        }

        public async Task<ClassUserResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetClassUserByIdAsync(id);
            if (item == null)
                return null;

            return new ClassUserResponseDto
            {
                Id = item.Id,
                ClassId = item.ClassId,
                UserId = item.UserId,
                Status = item.Status
            };
        }

        public async Task<ClassUserResponseDto?> CreateAsync(ClassUserCreateDto dto)
        {
            var entity = new ClassUser
            {
                ClassId = dto.ClassId,
                UserId = dto.UserId,
                Status = dto.Status
            };

            var created = await _repository.CreateClassUserAsync(entity);
            if (created == null)
                return null;

            return new ClassUserResponseDto
            {
                Id = created.Id,
                ClassId = created.ClassId,
                UserId = created.UserId,
                Status = created.Status
            };
        }

        public async Task<ClassUserResponseDto?> UpdateAsync(int id, ClassUserCreateDto dto)
        {
            var entity = new ClassUser
            {
                ClassId = dto.ClassId,
                UserId = dto.UserId,
                Status = dto.Status
            };

            var updated = await _repository.UpdateClassUserAsync(id, entity);
            if (updated == null)
                return null;

            return new ClassUserResponseDto
            {
                Id = updated.Id,
                ClassId = updated.ClassId,
                UserId = updated.UserId,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteClassUserAsync(id);
            return deleted != null;
        }
    }
}
