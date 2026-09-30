using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Domain.Interfaces.Repositories;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Project.Application.Services;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly ProjectDACNDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AuthController(
            IUserRepository userRepository,
            IJwtService jwtService,
            ProjectDACNDbContext dbContext,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        /// <summary>
        /// Login endpoint - nhận username/email/phone và password, trả về JWT token
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userRepository.GetByKeyAndPasswordAsync(dto.Keyword, dto.Password);
            
            if (user == null)
            {
                return Unauthorized(ApiResponse<string>.Fail("Invalid credentials"));
            }

            if (user.Status != 1)
            {
                return Unauthorized(ApiResponse<string>.Fail("User account is disabled"));
            }

            // Lấy danh sách PermissionId của user
            var permissionIds = await _dbContext.UserPermissions
                .Where(up => up.UserId == user.Id)
                .Select(up => up.PermissionId)
                .ToListAsync();

            // Generate AccessToken (JWT chứa LevelId + PermissionId)
            var accessToken = _jwtService.GenerateToken(user.Id, user.UserName, user.LevelId, permissionIds);

            // Generate RefreshToken và lưu DB
            var refreshToken = GenerateSecureRefreshToken();
            var refreshExpiryMinutes = int.Parse(_configuration["JwtSettings:RefreshTokenExpiryMinutes"] ?? "43200"); // default 30 ngày

            // Revoke toàn bộ refresh token cũ của user để tránh lạm dụng
            var oldTokens = _dbContext.RefreshTokens.Where(r => r.UserId == user.Id && !r.IsRevoked);
            foreach (var old in oldTokens)
            {
                old.IsRevoked = true;
            }

            await _dbContext.RefreshTokens.AddAsync(new Project.Domain.Entities.RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMinutes(refreshExpiryMinutes),
                IsRevoked = false
            });

            await _dbContext.SaveChangesAsync();

            return Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                LevelId = user.LevelId,
                PermissionIds = permissionIds
            }));
        }

        /// <summary>
        /// Refresh Token:
        /// - Nhận vào RefreshToken còn sống
        /// - Kiểm tra trong DB:
        ///     + Tồn tại
        ///     + Chưa hết hạn
        ///     + Chưa bị revoke
        /// - Nếu OK: sinh AccessToken + RefreshToken mới
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(ApiResponse<string>.Fail("Refresh token không hợp lệ"));
            }

            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

            if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiryDate <= DateTime.UtcNow)
            {
                return Unauthorized(ApiResponse<string>.Fail("Refresh token đã hết hạn hoặc không hợp lệ"));
            }

            var user = await _userRepository.GetByIdUserSendMailAsync(storedToken.UserId);
            if (user == null || user.Status != 1)
            {
                return Unauthorized(ApiResponse<string>.Fail("Tài khoản không còn hoạt động"));
            }

            var permissionIds = await _dbContext.UserPermissions
                .Where(up => up.UserId == user.Id)
                .Select(up => up.PermissionId)
                .ToListAsync();

            // Revoke token cũ
            storedToken.IsRevoked = true;

            // Tạo token mới
            var accessToken = _jwtService.GenerateToken(user.Id, user.UserName, user.LevelId, permissionIds);
            var newRefreshToken = GenerateSecureRefreshToken();
            var refreshExpiryMinutes = int.Parse(_configuration["JwtSettings:RefreshTokenExpiryMinutes"] ?? "43200"); // default 30 ngày

            await _dbContext.RefreshTokens.AddAsync(new Project.Domain.Entities.RefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMinutes(refreshExpiryMinutes),
                IsRevoked = false
            });

            await _dbContext.SaveChangesAsync();

            return Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                LevelId = user.LevelId,
                PermissionIds = permissionIds
            }));
        }

        /// <summary>
        /// Test endpoint để verify JWT token - yêu cầu authentication
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var levelId = User.FindFirstValue("level_id");
            var permissionIds = User.FindAll("permission").Select(c => c.Value).ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                UserId = userId,
                UserName = userName,
                LevelId = levelId,
                Permissions = permissionIds
            }));
        }

        /// <summary>
        /// Sinh refresh token ngẫu nhiên, đủ dài, dùng cho DB.
        /// </summary>
        private static string GenerateSecureRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

    public class LoginDto
    {
        public string Keyword { get; set; } = null!; // Username, Email, or PhoneNumber
        public string Password { get; set; } = null!;
    }

    public class LoginResponseDto
    {
        /// <summary>
        /// JWT Access Token dùng để gọi API (gắn vào header Authorization: Bearer {token})
        /// </summary>
        public string AccessToken { get; set; } = null!;

        /// <summary>
        /// Refresh Token lưu ở client (vd: httpOnly cookie) để xin AccessToken mới khi hết hạn.
        /// </summary>
        public string RefreshToken { get; set; } = null!;

        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int LevelId { get; set; }

        /// <summary>
        /// Danh sách PermissionId của user hiện tại.
        /// </summary>
        public List<int> PermissionIds { get; set; } = new();
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = null!;
    }
}
