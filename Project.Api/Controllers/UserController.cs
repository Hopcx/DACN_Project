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
using Project.Api.Services;
using Project.Domain.Entities;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/users")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ProjectDACNDbContext _db;
        private readonly IVerificationEmailSender _email;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UserController> _logger;
        public UserController(IUserService userService, ProjectDACNDbContext db,
            IVerificationEmailSender email, IConfiguration configuration, IWebHostEnvironment environment,
            ILogger<UserController> logger)
        {
            _userService = userService;
            _db = db;
            _email = email;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
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
        public async Task<IActionResult> GetAllUserAsync([FromQuery] string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                if (term.Length > 100)
                    return BadRequest(ApiResponse<string>.Fail("Từ khóa tìm kiếm quá dài"));
                var matched = await _db.Users.AsNoTracking()
                    .Where(x => x.FullName.Contains(term) || x.UserName.Contains(term) || x.Email.Contains(term))
                    .OrderBy(x => x.UserName).ThenBy(x => x.Id)
                    .ToListAsync();
                return Ok(ApiResponse<List<UserResponseDto>>.Ok(matched.Select(UserAccountMapper.ToDto).ToList()));
            }
            var result = await _userService.GetAllUsserAsync();
            return Ok(ApiResponse<List<UserResponseDto>>.Ok(result));
        }
        [HttpDelete("delete-user-{id}")]
        public async Task<IActionResult> DeleteUsserAsync(string id)
        {
            if (!Guid.TryParse(id, out var userId))
                return BadRequest(ApiResponse<string>.Fail("ID tài khoản không hợp lệ"));
            if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == id ||
                User.FindFirst("sub")?.Value == id)
                return Conflict(ApiResponse<string>.Fail("Không thể tự xóa tài khoản đang đăng nhập"));
            if (await _db.Users.AnyAsync(x => x.Id == userId && x.LevelId == 1) &&
                await _db.Users.CountAsync(x => x.LevelId == 1 && x.Status == 1) <= 1)
                return Conflict(ApiResponse<string>.Fail("Không thể xóa tài khoản quản trị cuối cùng"));
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
            if (await _db.Users.AnyAsync(x => x.UserName == dto.UserName ||
                x.Email == dto.Email || x.PhoneNumber == dto.PhoneNumber))
                return Conflict(ApiResponse<string>.Fail("Tên đăng nhập, email hoặc số điện thoại đã được sử dụng"));
            // ===== CÁCH CŨ: Manual check và return (đã comment) =====
            //var result = await _userService.CreateUserAsync(dto);
            //if (result == null)
            //    return BadRequest(ApiResponse<string>.Fail("Tạo User thất bại"));
            //return Created("", ApiResponse<UserResponseDto>.Ok(result));

            // ===== CÁCH MỚI: FluentValidation tự động validate, service throw exception nếu fail =====
            // FluentValidation sẽ tự động validate dto trước khi vào method này
            var rawToken = AuthTokenTools.NewToken();
            UserResponseDto result;
            await using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                result = await _userService.CreateUserAsync(dto);
                _db.EmailVerificationTokens.Add(new EmailVerificationToken
                {
                    UserId = result.Id, TokenHash = AuthTokenTools.Hash(rawToken),
                    CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24)
                });
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            var baseUrl = _configuration["Mail:PublicFrontendUrl"] ??
                (_environment.IsDevelopment() ? "https://localhost:5173" :
                    throw new InvalidOperationException("Missing public frontend URL"));
            var verificationUrl = $"{baseUrl.TrimEnd('/')}/auth/verify-email#token={Uri.EscapeDataString(rawToken)}";
            try { await _email.SendAsync(result.Email, verificationUrl); }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not send admin-created account verification: {ErrorType}", ex.GetType().Name);
                return Created("", ApiResponse<UserResponseDto>.Ok(result,
                    "Đã tạo tài khoản nhưng chưa gửi được email; hãy dùng chức năng gửi lại xác minh"));
            }
            return Created("", ApiResponse<UserResponseDto>.Ok(result, "Đã tạo tài khoản; cần xác minh email trước khi đăng nhập"));
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
