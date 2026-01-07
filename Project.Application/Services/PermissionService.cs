using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.PermissionDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IPermissionRepository _repository;
        public PermissionService(IPermissionRepository repository)
        {
            _repository = repository;
        }

        public async Task<PermissionResponseDto> CreatePermissionsAsync(PermissionCreateDto dto)
        {
            var permission = new Permission
            {
                Name = dto.Name,
                Status = dto.Status,
                Description = dto.Description
            };
            var createdPer = await _repository.CreatePermissionAsync(permission);
            if (createdPer == null)
                return null;
            return new PermissionResponseDto
            {
                Id = createdPer.Id,
                Name = createdPer.Name,
                Status = createdPer.Status,
                Description = createdPer.Description
            };
        }

        public async Task<bool> DeletePermissionAsync(int id)
        {
            var deletedPer = await _repository.DeletePermissionAsync(id);
            return deletedPer != null;
        }

        public async Task<List<PermissionResponseDto>> GetAllPermissionAsync()
        {
            var per = await _repository.GetAllPermissionAsync();

            return per.Select(p => new PermissionResponseDto
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status,
                Description = p.Description
            }).ToList();
        }

        public async Task<PermissionResponseDto> UpdatePermissionsAsync(int id, PermissionCreateDto dto)
        {
            var per = new Permission
            {
                Name = dto.Name,
                Status = dto.Status,
                Description = dto.Description
            };

            var updatedPer = await _repository.UpdatePermissionAsync(id, per);
            if (updatedPer == null)
                return null;

            return new PermissionResponseDto
            {
                Id = updatedPer.Id,
                Name = updatedPer.Name,
                Status = updatedPer.Status,
                Description = updatedPer.Description
            };
        }
    }
}
