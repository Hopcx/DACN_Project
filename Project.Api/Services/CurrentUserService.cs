using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Project.Application.Common;

namespace Project.Api.Services
{
    /// <summary>
    /// Triển khai ICurrentUserService dựa trên IHttpContextAccessor.
    /// - Đọc thông tin từ JWT Claims đã được gắn vào HttpContext.User
    /// - Dùng DI để inject vào Controller / Application Service.
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public ClaimsPrincipal? Principal => User;

        public Guid? UserId
        {
            get
            {
                var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                if (Guid.TryParse(sub, out var id))
                {
                    return id;
                }

                return null;
            }
        }

        public string? UserName =>
            User?.FindFirstValue(ClaimTypes.Name)
            ?? User?.FindFirstValue(JwtRegisteredClaimNames.UniqueName);

        public int? LevelId
        {
            get
            {
                var levelClaim = User?.FindFirstValue("level_id");
                if (int.TryParse(levelClaim, out var levelId))
                {
                    return levelId;
                }

                return null;
            }
        }

        public IReadOnlyCollection<int> Permissions
        {
            get
            {
                var permissionClaims = User?.FindAll("permission") ?? Enumerable.Empty<Claim>();
                return permissionClaims
                    .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToArray();
            }
        }

        public bool HasPermission(int permissionId)
        {
            return Permissions.Contains(permissionId);
        }
    }
}

