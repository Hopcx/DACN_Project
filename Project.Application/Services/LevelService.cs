using Project.Application.DTOs.LevelDTO;
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
    public class LevelService : ILevelService
    {
        private readonly ILevelRepository _repository;

        public LevelService(ILevelRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<LevelResponseDto>> GetAllLevelAsync()
        {
            var levels = await _repository.GetAllLevelAsync();

            return levels.Select(l => new LevelResponseDto
            {
                Id = l.Id,
                Name = l.Name,
                Status = l.Status
            }).ToList();
        }

        public async Task<LevelResponseDto> CreateLevelAsync(LevelCreateDto dto)
        {
            var level = new Level
            {
                Name = dto.Name,
                Status = dto.Status
            };

            var createdLevel = await _repository.CreateLevelAsync(level);

            if (createdLevel == null)
                return null;

            return new LevelResponseDto
            {
                Id = createdLevel.Id,
                Name = createdLevel.Name,
                Status = createdLevel.Status
            };
        }

        

        public async Task<bool> DeleteLevelAsync(int id)
        {
            var deletedLevel = await _repository.DeleteLevelAsync(id);

            // Nếu repository trả về null, xóa thất bại
            return deletedLevel != null;
        }

        public async Task<List<UserLevelResponseDto>> GetUsersByLevelAsync(int levelId, string? textSearch)
        {
            var users = await _repository.GetUserByIdLevel(levelId, textSearch);

            return users.Select(u => new UserLevelResponseDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                LevelId = u.LevelId
            }).ToList();
        }

        public async Task<LevelResponseDto> UpdateLevelAsync(int id, LevelCreateDto dto)
        {
            var level = new Level
            {
                Name = dto.Name,
                Status = dto.Status
            };

            var updatedLevel = await _repository.UpdateLevelAsync(id, level);
            if (updatedLevel == null)
                return null;

            return new LevelResponseDto
            {
                Id = updatedLevel.Id,
                Name = updatedLevel.Name,
                Status = updatedLevel.Status
            };
        }

    }

}
