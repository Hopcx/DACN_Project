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
        /// Lấy danh sách Room có hỗ trợ lọc và phân trang.
        /// </summary>
        /// <param name="name">
        /// Tên Room dùng để tìm kiếm (không phân biệt hoa thường, tìm theo chứa).
        /// Truyền <c>null</c> để không lọc theo tên.
        /// </param>
        /// <param name="status">
        /// Trạng thái hoạt động của Room.
        /// Truyền <c>null</c> để lấy tất cả.
        /// </param>
        /// <param name="minCapacity">
        /// Số lượng sức chứa tối thiểu.
        /// Truyền <c>null</c> để không lọc theo điều kiện này.
        /// </param>
        /// <param name="maxCapacity">
        /// Số lượng sức chứa tối đa.
        /// Truyền <c>null</c> để không lọc theo điều kiện này.
        /// </param>
        /// <param name="page">
        /// Số trang cần lấy (bắt đầu từ 1).
        /// </param>
        /// <param name="pageSize">
        /// Số lượng Room trên mỗi trang.
        /// </param>
        /// <returns>
        /// Một tuple gồm:
        /// <list type="bullet">
        ///   <item>
        ///     <description><see cref="List{Room}"/>: danh sách Room theo điều kiện lọc và phân trang.</description>
        ///   </item>
        ///   <item>
        ///     <description><c>TotalCount</c>: tổng số Room thỏa mãn điều kiện lọc (không phân trang).</description>
        ///   </item>
        /// </list>
        /// </returns>
        Task<(List<Room> Rooms, int TotalCount)> GetRoomsAsync(
            string? name,
            bool? status,
            int? minCapacity,
            int? maxCapacity,
            int page,
            int pageSize
        );


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
