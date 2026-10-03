using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Domain.Interfaces.Repositories;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Project.Application.Services;
using Project.Infrastructure.Persistence;
using Project.Api.Filters;
using Project.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;

namespace Project.Api.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(AuthCsrfFilter))]
    [Route("web/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly ProjectDACNDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IAntiforgery _antiforgery;
        private const string RefreshCookie = "__Secure-dacn-refresh";

        public AuthController(
            IUserRepository userRepository,
            IJwtService jwtService,
            ProjectDACNDbContext dbContext,
            IConfiguration configuration,
            IAntiforgery antiforgery)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _dbContext = dbContext;
            _configuration = configuration;
            _antiforgery = antiforgery;
        }

        [HttpGet("csrf")]
        [AllowAnonymous]
        public IActionResult Csrf()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(ApiResponse<object>.Ok(new { csrfToken = tokens.RequestToken }));
        }

        /// <summary>
        /// Login endpoint - nhận username/email/phone và password, trả về JWT token
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth-public")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userRepository.GetByKeyAndPasswordAsync(dto.Keyword, dto.Password);
            
            if (user == null)
            {
                return Unauthorized(ApiResponse<string>.Fail("Invalid credentials"));
            }

            if (user.Status != 1 || user.EmailVerifiedAt == null)
            {
                return Unauthorized(ApiResponse<string>.Fail("Tài khoản chưa xác minh hoặc không hoạt động"));
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
            var refreshExpiryMinutes = RefreshExpiryMinutes();

            // Revoke toàn bộ refresh token cũ của user để tránh lạm dụng
            await using var loginTransaction = await _dbContext.Database.BeginTransactionAsync();
            await _dbContext.RefreshTokens.Where(r => r.UserId == user.Id && !r.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true));

            await _dbContext.RefreshTokens.AddAsync(new Project.Domain.Entities.RefreshToken
            {
                UserId = user.Id,
                TokenHash = AuthTokenTools.Hash(refreshToken),
                FamilyId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMinutes(refreshExpiryMinutes),
                IsRevoked = false
            });

            await _dbContext.SaveChangesAsync();
            await loginTransaction.CommitAsync();
            SetRefreshCookie(refreshToken, refreshExpiryMinutes);

            return Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
            {
                AccessToken = accessToken,
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
        [EnableRateLimiting("auth-session")]
        public async Task<IActionResult> RefreshToken()
        {
            if (!Request.Cookies.TryGetValue(RefreshCookie, out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
            {
                return Unauthorized(ApiResponse<string>.Fail("Không có refresh cookie"));
            }

            var tokenHash = AuthTokenTools.Hash(rawToken);
            var storedToken = await _dbContext.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

            if (storedToken == null || storedToken.ExpiryDate <= DateTime.UtcNow)
            {
                DeleteRefreshCookie();
                return Unauthorized(ApiResponse<string>.Fail("Refresh token đã hết hạn hoặc không hợp lệ"));
            }
            if (storedToken.IsRevoked)
            {
                await _dbContext.RefreshTokens.Where(r => r.FamilyId == storedToken.FamilyId && !r.IsRevoked)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true));
                DeleteRefreshCookie();
                return Unauthorized(ApiResponse<string>.Fail("Refresh token đã được sử dụng lại"));
            }

            var user = await _userRepository.GetByIdUserSendMailAsync(storedToken.UserId);
            if (user == null || user.Status != 1 || user.EmailVerifiedAt == null)
            {
                DeleteRefreshCookie();
                return Unauthorized(ApiResponse<string>.Fail("Tài khoản không còn hoạt động"));
            }

            var permissionIds = await _dbContext.UserPermissions
                .Where(up => up.UserId == user.Id)
                .Select(up => up.PermissionId)
                .ToListAsync();

            // Conditional update serializes concurrent use of the same refresh token.
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var revoked = await _dbContext.RefreshTokens
                .Where(r => r.Id == storedToken.Id && !r.IsRevoked && r.ExpiryDate > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsRevoked, true));
            if (revoked != 1)
            {
                await _dbContext.RefreshTokens.Where(r => r.FamilyId == storedToken.FamilyId && !r.IsRevoked)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true));
                await transaction.CommitAsync();
                DeleteRefreshCookie();
                return Unauthorized(ApiResponse<string>.Fail("Refresh token đã được sử dụng hoặc thu hồi"));
            }

            var accessToken = _jwtService.GenerateToken(user.Id, user.UserName, user.LevelId, permissionIds);
            var newRefreshToken = GenerateSecureRefreshToken();
            var refreshExpiryMinutes = RefreshExpiryMinutes();

            await _dbContext.RefreshTokens.AddAsync(new Project.Domain.Entities.RefreshToken
            {
                UserId = user.Id,
                TokenHash = AuthTokenTools.Hash(newRefreshToken),
                FamilyId = storedToken.FamilyId,
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMinutes(refreshExpiryMinutes),
                IsRevoked = false
            });

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            SetRefreshCookie(newRefreshToken, refreshExpiryMinutes);

            return Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
            {
                AccessToken = accessToken,
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
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var id) ||
                !await _dbContext.Users.AnyAsync(u => u.Id == id && u.Status == 1))
                return Unauthorized(ApiResponse<string>.Fail("Tài khoản không còn hoạt động"));
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

        [HttpPost("logout")]
        [Authorize]
        [EnableRateLimiting("auth-session")]
        public async Task<IActionResult> Logout()
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var bearer = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(jti) || !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Unauthorized(ApiResponse<string>.Fail("JWT không có jti/exp"));
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(bearer[7..].Trim());
            if (jwt.Id != jti || jwt.ValidTo <= DateTime.UtcNow)
                return Unauthorized(ApiResponse<string>.Fail("JWT không hợp lệ"));
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            _dbContext.BlackListTokens.Add(new Project.Domain.Entities.BlackListToken
            {
                Token = jti,
                ExpiryDate = jwt.ValidTo,
                BlacklistAt = DateTime.UtcNow
            });
            await _dbContext.RefreshTokens.Where(r => r.UserId == userId && !r.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsRevoked, true));
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            DeleteRefreshCookie();
            return Ok(ApiResponse<string>.Ok(string.Empty, "Đã thu hồi các phiên làm mới"));
        }

        private int RefreshExpiryMinutes() =>
            int.Parse(_configuration["JwtSettings:RefreshTokenExpiryMinutes"] ?? "43200");

        private void SetRefreshCookie(string token, int expiryMinutes) => Response.Cookies.Append(
            RefreshCookie, token, new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                Path = "/web/auth", MaxAge = TimeSpan.FromMinutes(expiryMinutes)
            });

        private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookie, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/web/auth"
        });

        /// <summary>
        /// Sinh refresh token ngẫu nhiên, đủ dài, dùng cho DB.
        /// </summary>
        private static string GenerateSecureRefreshToken()
        {
            return AuthTokenTools.NewToken();
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
        /// Refresh token chỉ được đặt trong cookie HttpOnly, không có trong JSON response.
        /// </summary>
        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int LevelId { get; set; }

        /// <summary>
        /// Danh sách PermissionId của user hiện tại.
        /// </summary>
        public List<int> PermissionIds { get; set; } = new();
    }

}
