using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Project.Application.Services
{
    /// <summary>
    /// Service để xử lý JWT token generation và validation
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Tạo AccessToken chứa đầy đủ thông tin user:
        /// - Id
        /// - UserName
        /// - LevelId
        /// - Danh sách PermissionId
        /// </summary>
        /// <param name="userId">Id của user</param>
        /// <param name="userName">UserName hiển thị</param>
        /// <param name="levelId">LevelId (Admin, Examiner, Teacher, Student)</param>
        /// <param name="permissionIds">Danh sách PermissionId được gán cho user</param>
        /// <returns>Chuỗi JWT Access Token</returns>
        string GenerateToken(Guid userId, string userName, int levelId, IEnumerable<int> permissionIds);

        ClaimsPrincipal? ValidateToken(string token);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Sinh AccessToken JWT:
        /// - Thời gian sống: JwtSettings:ExpiryMinutes
        /// - Claim chuẩn: sub, unique_name, jti
        /// - Claim custom:
        ///     + level_id     : LevelId của user
        ///     + permission   : lặp lại cho từng PermissionId (policy-based Authorization sẽ đọc claim này)
        /// </summary>
        public string GenerateToken(Guid userId, string userName, int levelId, IEnumerable<int> permissionIds)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
            var issuer = jwtSettings["Issuer"] ?? "ProjectDACN";
            var audience = jwtSettings["Audience"] ?? "ProjectDACN";
            var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "60");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Các claim cơ bản của user
            var claims = new List<Claim>
            {
                // Id user
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),

                // Tên hiển thị
                new Claim(JwtRegisteredClaimNames.UniqueName, userName),

                // LevelId dùng để phân quyền theo Level (Admin / Examiner / Teacher / Student)
                new Claim("level_id", levelId.ToString()),

                // Mã duy nhất của token
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Thêm claim PermissionId: mỗi permission tạo 1 claim "permission" riêng
            // Ví dụ: permission = 1 -> "Quản lý bài thi"
            if (permissionIds != null)
            {
                foreach (var permissionId in permissionIds.Distinct())
                {
                    claims.Add(new Claim("permission", permissionId.ToString()));
                }
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }
    }
}
