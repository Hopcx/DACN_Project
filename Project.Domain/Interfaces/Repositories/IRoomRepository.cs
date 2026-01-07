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
        /// <summary>
        /// Xem danh sách toàn bộ Room.
        /// </summary>
        /// <returns>Danh sách Room.</returns>
        Task<List<Room>> GetAllRoomAsync();

        /// <summary>
        /// Xem thông tin chi tiết Room theo Id.
        /// </summary>
        /// <param name="id">Id của Room.</param>
        /// <returns>Room tương ứng hoặc null nếu không tồn tại.</returns>
        Task<Room> GetRoomByIdAsync(int id);

        /// <summary>
        /// Tạo mới Room.
        /// </summary>
        /// <param name="r">Dữ liệu Room cần tạo.</param>
        /// <returns>Room vừa được tạo.</returns>
        Task<Room> CreateRoomAsync(Room r);

        /// <summary>
        /// Cập nhật Room theo Id.
        /// </summary>
        /// <param name="id">Id của Room cần cập nhật.</param>
        /// <param name="r">Dữ liệu Room mới.</param>
        /// <returns>Room sau khi cập nhật hoặc null nếu không tồn tại.</returns>
        Task<Room> UpdateRoomAsync(int id, Room r);

        /// <summary>
        /// Xóa Room theo Id.
        /// </summary>
        /// <param name="id">Id của Room cần xóa.</param>
        /// <returns>Room đã bị xóa hoặc null nếu không tồn tại.</returns>
        Task<Room> DeleteRoomAsync(int id);

    }
}
