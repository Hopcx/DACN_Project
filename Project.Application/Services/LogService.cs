using Project.Application.DTOs.LogDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class LogService : ILogService
    {
        private readonly ILogRepository _repository;

        public LogService(ILogRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<LogResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllLogsAsync();
            return items.Select(x => new LogResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status
            }).ToList();
        }

        public async Task<LogResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetLogByIdAsync(id);
            if (item == null)
                return null;

            return new LogResponseDto
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Status = item.Status
            };
        }

        public async Task<LogResponseDto?> CreateAsync(LogCreateDto dto)
        {
            var entity = new Log
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var created = await _repository.CreateLogAsync(entity);
            if (created == null)
                return null;

            return new LogResponseDto
            {
                Id = created.Id,
                Name = created.Name,
                Description = created.Description,
                Status = created.Status
            };
        }

        public async Task<LogResponseDto?> UpdateAsync(int id, LogCreateDto dto)
        {
            var entity = new Log
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var updated = await _repository.UpdateLogAsync(id, entity);
            if (updated == null)
                return null;

            return new LogResponseDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteLogAsync(id);
            return deleted != null;
        }
    }
}
