using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.RoomDTO;
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
    public class RoomService : IRoomService
    {
        private readonly IRoomRepository _repository;
        public RoomService(IRoomRepository repository)
        {
            _repository = repository;
        }
        public async Task<RoomResponseDto> CreateAsync(RoomCreateDto dto)
        {
            var room = new Room
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Address = dto.Address,
                Status = dto.Status ?? true
            };
            await _repository.AddAsync(room);
            return new RoomResponseDto
            {
                Id = room.Id,
                Name = room.Name,
                Capacity = room.Capacity,
                Address = room.Address,
                Status = room.Status
            };
        }

        public async Task<List<RoomResponseDto>> GetAllAsync()
        {
            var room = await _repository.GetAllAsync();

            return room.Select(r => new RoomResponseDto
            {
                Id = r.Id,
                Name = r.Name,
                Capacity = r.Capacity,
                Address = r.Address,
                Status = r.Status
            }).ToList();
        }
    }
}
