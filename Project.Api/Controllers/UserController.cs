using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.RoomDTO;
using Project.Application.DTOs.UserDTO;
using Project.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/users")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ProjectDACNDbContext _db;
        public UserController(IUserService userService, ProjectDACNDbContext db)
        {
            _userService = userService;
            _db = db;
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return user == null ? NotFound(ApiResponse<string>.Fail("Không tìm thấy tài khoản"))
                : Ok(ApiResponse<UserResponseDto>.Ok(UserAccountMapper.ToDto(user)));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AdminUserUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Address))
                return BadRequest(ApiResponse<string>.Fail("Họ tên và địa chỉ không được để trống"));
            var userName = request.UserName.Trim();
            var email = request.Email.Trim();
            var phone = request.PhoneNumber.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null) return NotFound(ApiResponse<string>.Fail("Không tìm thấy tài khoản"));
            if (await _db.Users.AnyAsync(x => x.Id != id &&
                (x.UserName == userName || x.Email == email || x.PhoneNumber == phone)))
                return Conflict(ApiResponse<string>.Fail("Tên đăng nhập, email hoặc số điện thoại đã được sử dụng"));

            user.FullName = request.FullName.Trim();
            user.UserName = userName;
            user.Email = email;
            user.PhoneNumber = phone;
            user.Address = request.Address.Trim();
            user.AvatarUrl = request.AvatarUrl;
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                return Conflict(ApiResponse<string>.Fail("Tên đăng nhập, email hoặc số điện thoại đã được sử dụng"));
            }
            return Ok(ApiResponse<UserResponseDto>.Ok(UserAccountMapper.ToDto(user)));
        }

        [HttpGet("get-all-users")]
        public async Task<IActionResult> GetAllUserAsync()
        {
            var result = await _userService.GetAllUsserAsync();
            return Ok(ApiResponse<List<UserResponseDto>>.Ok(result));
        }
        [HttpDelete("delete-user-{id}")]
        public async Task<IActionResult> DeleteUsserAsync(string id)
        {
            // ===== CÁCH CŨ: Manual check và return (đã comment) =====
            //var isDeleted = await _userService.DeleteUsserAsync(id);
            //if (!isDeleted)
            //{
            //    return NotFound(ApiResponse<string>.Fail("User không tồn tại hoặc xóa thất bại."));
            //}
            //return Ok(ApiResponse<string>.Ok(null, $"Xóa User với ID {id} thành công."));

            // ===== CÁCH MỚI: Service throw exception, middleware xử lý =====
            await _userService.DeleteUsserAsync(id);
            return Ok(ApiResponse<string>.Ok(null, $"Xóa User với ID {id} thành công."));
        }

        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUserAsync(UserCreateDto dto)
        {
            // ===== CÁCH CŨ: Manual check và return (đã comment) =====
            //var result = await _userService.CreateUserAsync(dto);
            //if (result == null)
            //    return BadRequest(ApiResponse<string>.Fail("Tạo User thất bại"));
            //return Created("", ApiResponse<UserResponseDto>.Ok(result));

            // ===== CÁCH MỚI: FluentValidation tự động validate, service throw exception nếu fail =====
            // FluentValidation sẽ tự động validate dto trước khi vào method này
            var result = await _userService.CreateUserAsync(dto);
            return Created("", ApiResponse<UserResponseDto>.Ok(result, "Create user succesfully"));
        }
    }

    public class AdminUserUpdateRequest
    {
        [Required, StringLength(100, MinimumLength = 1)] public string FullName { get; set; } = "";
        [Required, StringLength(100, MinimumLength = 6), RegularExpression("^[a-zA-Z0-9_]+$")]
        public string UserName { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, RegularExpression(@"^[0-9]{10,11}$")] public string PhoneNumber { get; set; } = "";
        [Required, StringLength(200, MinimumLength = 1)] public string Address { get; set; } = "";
        public string? AvatarUrl { get; set; }
    }
}
