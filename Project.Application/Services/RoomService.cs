using Project.Application.DTOs;
using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.RoomDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        public async Task<RoomResponseDto> CreateRoomAsync(RoomCreateDto dto)
        {
            var room = new Room
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Address = dto.Address,
                Status = dto.Status ?? true
            };

            var createdRoom = await _repository.CreateRoomAsync(room);

            if (createdRoom == null)
                return null;

            return new RoomResponseDto
            {
                Id = createdRoom.Id,
                Name = createdRoom.Name,
                Capacity = createdRoom.Capacity,
                Address = createdRoom.Address,
                Status = createdRoom.Status
            };
        }

        public async Task<bool> DeleteRoomAsync(int id)
        {
            var deletedLevel = await _repository.DeleteRoomAsync(id);

            // Nếu repository trả về null, xóa thất bại
            return deletedLevel != null;
        }

        public async Task<List<RoomResponseDto>> GetAllRoomAsync()
        {
            //var rooms = await _repository.GetAllRoomAsync();

            //return rooms.Select(r => new RoomResponseDto
            //{
            //    Id = r.Id,
            //    Name = r.Name,
            //    Capacity = r.Capacity,
            //    Address = r.Address,
            //    Status = r.Status
            //}).ToList();
            return null;
        }

        public async Task<RoomResponseDto?> GetRoomByIdAsync(int id)
        {
            // (Optional) validate input
            if (id <= 0)
                return null;

            var room = await _repository.GetRoomByIdAsync(id);

            if (room == null)
                return null;

            return new RoomResponseDto
            {
                Id = room.Id,
                Name = room.Name,
                Capacity = room.Capacity,
                Address = room.Address,
                Status = room.Status
            };
        }


        public async Task<PagedResult<RoomResponseDto>> GetRoomsAsync(RoomQueryDto query)
        {
            var (rooms, totalCount) = await _repository.GetRoomsAsync(
                query.Name,
                query.Status,
                query.MinCapacity,
                query.MaxCapacity,
                query.Page,
                query.PageSize
            );

            return new PagedResult<RoomResponseDto>
            {
                Items = rooms.Select(r => new RoomResponseDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Capacity = r.Capacity,
                    Address = r.Address,
                    Status = r.Status
                }).ToList(),
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<RoomResponseDto> UpdateRoomAsync(int id, RoomCreateDto dto)
        {
            var room = new Room
            {
                Name = dto.Name,
                Status = dto.Status,
                Capacity = dto.Capacity,
                Address = dto.Address
            };

            var updatedRoom = await _repository.UpdateRoomAsync(id, room);
            if (updatedRoom == null)
                return null;

            return new RoomResponseDto
            {
                Id = updatedRoom.Id,
                Name = updatedRoom.Name,
                Status = updatedRoom.Status,
                Capacity = updatedRoom.Capacity,
                Address = updatedRoom.Address
            };
        }
    }

}
