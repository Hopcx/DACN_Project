using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IRoomRepository
    {
        Task<List<Room>> GetAllRoomAsync();
        Task<Room> GetRoomByIdAsync(int id);
        Task<Room> CreateRoomAsync(Room r);
        Task<Room> UpdateRoomAsync(Room r);
        Task<Room> DeleteRoomAsync(int id);
    }
}
