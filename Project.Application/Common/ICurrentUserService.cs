using System.Security.Claims;

namespace Project.Application.Common
{
    /// <summary>
    /// Service dùng chung để đọc thông tin user hiện tại từ HttpContext (JWT Claims).
    /// Có thể inject vào Service / Controller để biết:
    /// - Ai đang đăng nhập (UserId)
    /// - LevelId hiện tại
    /// - Danh sách PermissionId
    /// </summary>
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        int? LevelId { get; }
        IReadOnlyCollection<int> Permissions { get; }
        string? UserName { get; }

        /// <summary>
        /// Kiểm tra user có permission cụ thể hay không.
        /// </summary>
        /// <param name="permissionId">Id quyền cần kiểm tra.</param>
        /// <returns>true nếu có; ngược lại false.</returns>
        bool HasPermission(int permissionId);

        /// <summary>
        /// Lấy toàn bộ ClaimsPrincipal hiện tại (nếu cần xử lý nâng cao).
        /// </summary>
        ClaimsPrincipal? Principal { get; }
    }
}

