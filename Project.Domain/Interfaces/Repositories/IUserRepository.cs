using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IUserRepository
    {
        /// <summary>
        /// Lấy danh sách tất cả người dùng.
        /// </summary>
        /// <returns>Danh sách người dùng.</returns>
        Task<List<User>> GetAllUsersAsync();

        /// <summary>
        /// Lấy thông tin người dùng theo ID (chuỗi).
        /// </summary>
        /// <param name="id">ID của người dùng.</param>
        /// <returns>Thông tin người dùng.</returns>
        Task<User> GetByIdUserAsync(string id);

        /// <summary>
        /// Lấy thông tin người dùng theo ID (Guid) để phục vụ gửi email.
        /// </summary>
        /// <param name="id">ID người dùng.</param>
        /// <returns>Thông tin người dùng.</returns>
        Task<User> GetByIdUserSendMailAsync(Guid id);

        /// <summary>
        /// Thêm mới một người dùng.
        /// </summary>
        /// <param name="user">Đối tượng người dùng.</param>
        /// <returns>Người dùng sau khi được thêm.</returns>
        Task<User> AddUserAsync(User user);

        /// <summary>
        /// Cập nhật thông tin người dùng.
        /// </summary>
        /// <param name="user">Đối tượng người dùng.</param>
        /// <returns>Người dùng sau khi cập nhật.</returns>
        Task<User> UpdateUserAsync(User user);

        /// <summary>
        /// Xóa người dùng theo ID.
        /// </summary>
        /// <param name="id">ID người dùng.</param>
        /// <returns>Người dùng đã bị xóa.</returns>
        Task<User> DeleteUserAsync(Guid id);

        /// <summary>
        /// Lấy người dùng theo từ khóa và mật khẩu đã mã hóa.
        /// </summary>
        /// <param name="keyword">Email, username hoặc số điện thoại.</param>
        /// <param name="hashPassword">Mật khẩu đã mã hóa.</param>
        /// <returns>Người dùng phù hợp.</returns>
        Task<User> GetByKeyAndPasswordAsync(string keyword, string hashPassword);

        /// <summary>
        /// Tìm người dùng đã tồn tại theo từ khóa.
        /// </summary>
        /// <param name="key">Email, username hoặc số điện thoại.</param>
        /// <returns>Người dùng nếu tồn tại, ngược lại trả về null.</returns>
        Task<User> FindUserExistByKeyWordAsync(string key);

        /// <summary>
        /// Lấy danh sách người dùng có trạng thái 1 trong lớp.
        /// </summary>
        /// <param name="classId">ID lớp học.</param>
        /// <param name="searchValue">Giá trị tìm kiếm.</param>
        /// <returns>Danh sách người dùng.</returns>
        Task<List<User>> GetUsersWithStatusOneAsync(int classId, string? searchValue);

        /// <summary>
        /// Lấy danh sách người dùng có trạng thái 2 trong lớp.
        /// </summary>
        /// <param name="classId">ID lớp học.</param>
        /// <returns>Danh sách người dùng.</returns>
        Task<List<User>> GetUsersWithStatusTwoAsync(int classId);

        /// <summary>
        /// Kiểm tra email, số điện thoại hoặc username đã tồn tại hay chưa.
        /// </summary>
        /// <param name="email">Email.</param>
        /// <param name="phoneNumber">Số điện thoại.</param>
        /// <param name="userName">Tên đăng nhập.</param>
        /// <param name="userId">ID người dùng (bỏ qua khi cập nhật).</param>
        /// <returns>
        /// True nếu đã tồn tại, ngược lại là false.
        /// </returns>
        Task<bool> CheckEmailOrPhoneAsync(string email, string phoneNumber, string userName, Guid? userId);

        /// <summary>
        /// Lấy danh sách người dùng theo LevelId.
        /// </summary>
        /// <param name="levelId">ID cấp độ.</param>
        /// <returns>Danh sách người dùng.</returns>
        Task<List<User>> GetUsersByLevelIdAsync(int levelId);

        /// <summary>
        /// Lấy danh sách người dùng chưa thuộc lớp chỉ định.
        /// </summary>
        /// <param name="classId">ID lớp học.</param>
        /// <param name="textSearch">Từ khóa tìm kiếm.</param>
        /// <returns>Danh sách người dùng.</returns>
        Task<List<User>> GetUsersNotInClassAsync(int classId, string? textSearch);

    }
}
