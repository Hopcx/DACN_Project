using Project.Application.DTOs;
using Project.Application.DTOs.RoomDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IRoomService
    {
        Task<List<RoomResponseDto>> GetAllRoomAsync();
        Task<RoomResponseDto> CreateRoomAsync(RoomCreateDto dto);
        Task<RoomResponseDto> GetRoomByIdAsync(int id);
        Task<RoomResponseDto> UpdateRoomAsync(int id, RoomCreateDto dto);
        Task<bool> DeleteRoomAsync(int id);
        Task<PagedResult<RoomResponseDto>> GetRoomsAsync(RoomQueryDto query);
    }
}
