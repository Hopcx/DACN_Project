using Project.Application.DTOs.RoomDTO;
using Project.Application.DTOs.UserDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IUserService
    {
        Task<List<UserResponseDto>> GetAllUsserAsync();
        Task<UserResponseDto> CreateRoomAsync(UserCreateDto dto);
        Task<bool> DeleteUsserAsync(string id);
    }
}
