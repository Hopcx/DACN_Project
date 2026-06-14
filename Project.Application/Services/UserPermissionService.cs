using Project.Application.DTOs.UserPermissionDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class UserPermissionService : IUserPermissionService
    {
        private readonly IUserPermissionRepository _repository;

        public UserPermissionService(IUserPermissionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<UserPermissionResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllUserPermissionsAsync();
            return items.Select(x => new UserPermissionResponseDto
            {
                Id = x.Id,
                UserId = x.UserId,
                PermissionId = x.PermissionId
            }).ToList();
        }

        public async Task<UserPermissionResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetUserPermissionByIdAsync(id);
            if (item == null)
                return null;

            return new UserPermissionResponseDto
            {
                Id = item.Id,
                UserId = item.UserId,
                PermissionId = item.PermissionId
            };
        }

        public async Task<UserPermissionResponseDto?> CreateAsync(UserPermissionCreateDto dto)
        {
            var entity = new UserPermission
            {
                UserId = dto.UserId,
                PermissionId = dto.PermissionId
            };

            var created = await _repository.CreateUserPermissionAsync(entity);
            if (created == null)
                return null;

            return new UserPermissionResponseDto
            {
                Id = created.Id,
                UserId = created.UserId,
                PermissionId = created.PermissionId
            };
        }

        public async Task<UserPermissionResponseDto?> UpdateAsync(int id, UserPermissionCreateDto dto)
        {
            var entity = new UserPermission
            {
                UserId = dto.UserId,
                PermissionId = dto.PermissionId
            };

            var updated = await _repository.UpdateUserPermissionAsync(id, entity);
            if (updated == null)
                return null;

            return new UserPermissionResponseDto
            {
                Id = updated.Id,
                UserId = updated.UserId,
                PermissionId = updated.PermissionId
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteUserPermissionAsync(id);
            return deleted != null;
        }
    }
}
