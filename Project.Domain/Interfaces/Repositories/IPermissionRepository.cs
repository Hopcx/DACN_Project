using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IPermissionRepository
    {
        /// <summary>
        /// Xem danh sách toàn bộ Permission.
        /// </summary>
        /// <returns>Danh sách Permission.</returns>
        Task<List<Permission>> GetAllPermissionAsync();

        /// <summary>
        /// Xem thông tin chi tiết Permission theo Id.
        /// </summary>
        /// <param name="id">Id của Permission.</param>
        /// <returns>Permission tương ứng hoặc null nếu không tồn tại.</returns>
        Task<Permission> GetPermissionByIdAsync(int id);

        /// <summary>
        /// Tạo mới Permission.
        /// </summary>
        /// <param name="permission">Dữ liệu Permission cần tạo.</param>
        /// <returns>Permission vừa được tạo.</returns>
        Task<Permission> CreatePermissionAsync(Permission permission);

        /// <summary>
        /// Cập nhật Permission theo Id.
        /// </summary>
        /// <param name="id">Id của Permission cần cập nhật.</param>
        /// <param name="r">Dữ liệu Permission mới.</param>
        /// <returns>Permission sau khi cập nhật hoặc null nếu không tồn tại.</returns>
        Task<Permission> UpdatePermissionAsync(int id, Permission r);

        /// <summary>
        /// Xóa Permission theo Id.
        /// </summary>
        /// <param name="id">Id của Permission cần xóa.</param>
        /// <returns>Permission đã bị xóa hoặc null nếu không tồn tại.</returns>
        Task<Permission> DeletePermissionAsync(int id);

    }
}
