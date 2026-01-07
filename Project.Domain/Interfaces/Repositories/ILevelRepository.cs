using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface ILevelRepository
    {
        /// <summary>
        /// Xem danh sách toàn bộ Level.
        /// </summary>
        /// <returns>Danh sách Level.</returns>
        Task<List<Level>> GetAllLevelAsync();

        /// <summary>
        /// Xem thông tin chi tiết Level theo Id.
        /// </summary>
        /// <param name="id">Id của Level.</param>
        /// <returns>Level tương ứng hoặc null nếu không tồn tại.</returns>
        Task<Level> GetLevelByIdAsync(int id);

        /// <summary>
        /// Tạo mới Level.
        /// </summary>
        /// <param name="r">Dữ liệu Level cần tạo.</param>
        /// <returns>Level vừa được tạo.</returns>
        Task<Level> CreateLevelAsync(Level r);

        /// <summary>
        /// Cập nhật Level theo Id.
        /// </summary>
        /// <param name="id">Id của Level cần cập nhật.</param>
        /// <param name="r">Dữ liệu Level mới.</param>
        /// <returns>Level sau khi cập nhật hoặc null nếu không tồn tại.</returns>
        Task<Level> UpdateLevelAsync(int id, Level r);

        /// <summary>
        /// Xóa Level theo Id.
        /// </summary>
        /// <param name="id">Id của Level cần xóa.</param>
        /// <returns>Level đã bị xóa hoặc null nếu không tồn tại.</returns>
        Task<Level> DeleteLevelAsync(int id);

        /// <summary>
        /// Xem danh sách User theo LevelId, có hỗ trợ tìm kiếm.
        /// </summary>
        /// <param name="levelId">Id của Level.</param>
        /// <param name="textSearch">Từ khóa tìm kiếm.</param>
        /// <returns>Danh sách User thỏa điều kiện.</returns>
        Task<List<User>> GetUserByIdLevel(int levelId, string? textSearch);

    }
}
