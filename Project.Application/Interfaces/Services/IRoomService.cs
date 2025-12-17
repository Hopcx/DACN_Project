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
        Task<List<RoomResponseDto>> GetAllAsync();
        Task<RoomResponseDto> CreateAsync(RoomCreateDto dto);
    }
}
