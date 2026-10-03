using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Project.Api.Filters;
using Project.Api.Services;
using Project.Application.Common;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(AuthCsrfFilter))]
[Route("web/auth")]
public class RegistrationController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;
    private readonly IVerificationEmailSender _email;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(ProjectDACNDbContext db, IVerificationEmailSender email,
        IConfiguration config, IWebHostEnvironment environment, ILogger<RegistrationController> logger)
    {
        _db = db;
        _email = email;
        _config = config;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-public")]
    public async Task<IActionResult> Register([FromBody] RegisterStudentRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var userName = request.UserName.Trim();
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Address) ||
            userName.Length < 6 || request.DateOfBirth == default ||
            request.DateOfBirth.Date > DateTime.UtcNow.Date ||
            request.DateOfBirth.Date < new DateTime(1753, 1, 1) ||
            await _db.Users.AnyAsync(x => x.Email.ToLower() == email || x.UserName == userName ||
                (phone != null && x.PhoneNumber == phone)))
            return Conflict(ApiResponse<string>.Fail("Thông tin đăng ký không hợp lệ hoặc đã được sử dụng"));

        var studentLevelId = await _db.Levels.Where(x => x.Name == "Student" && x.Status == 1)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (studentLevelId == null)
            return StatusCode(503, ApiResponse<string>.Fail("Chưa cấu hình vai trò Student"));

        var user = new User
        {
            Id = Guid.NewGuid(), FullName = request.FullName.Trim(), UserName = userName,
            Email = email, PhoneNumber = phone, Address = request.Address.Trim(),
            DateOfBirth = request.DateOfBirth, Sex = request.Sex!.Value, Status = 1,
            LevelId = studentLevelId.Value,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            LastVerificationSentAt = DateTime.UtcNow
        };
        var rawToken = AuthTokenTools.NewToken();
        await using (var transaction = await _db.Database.BeginTransactionAsync())
        {
            _db.Users.Add(user);
            _db.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId = user.Id, TokenHash = AuthTokenTools.Hash(rawToken),
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                return Conflict(ApiResponse<string>.Fail("Thông tin đăng ký không hợp lệ hoặc đã được sử dụng"));
            }
            await transaction.CommitAsync();
        }
        await SendVerificationAsync(email, rawToken);
        return Accepted(ApiResponse<string>.Ok(string.Empty, "Nếu đăng ký hợp lệ, vui lòng kiểm tra email để xác minh"));
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("auth-public")]
    public async Task<IActionResult> Verify([FromBody] VerifyEmailRequest request)
    {
        var hash = AuthTokenTools.Hash(request.Token);
        var token = await _db.EmailVerificationTokens.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (token == null || token.ConsumedAt != null || token.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(ApiResponse<string>.Fail("Token xác minh không hợp lệ hoặc đã hết hạn"));

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var updated = await _db.EmailVerificationTokens.Where(x => x.Id == token.Id &&
                x.ConsumedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, DateTime.UtcNow));
        if (updated != 1)
            return BadRequest(ApiResponse<string>.Fail("Token xác minh đã được sử dụng"));
        await _db.Users.Where(x => x.Id == token.UserId && x.EmailVerifiedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EmailVerifiedAt, DateTime.UtcNow));
        await transaction.CommitAsync();
        return Ok(ApiResponse<string>.Ok(string.Empty, "Email đã được xác minh"));
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth-public")]
    public async Task<IActionResult> Resend([FromBody] ResendVerificationRequest request)
    {
        const string message = "Nếu email có tài khoản chưa xác minh, chúng tôi sẽ gửi liên kết mới";
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email.ToLower() == email &&
            x.EmailVerifiedAt == null && x.Status == 1);
        if (user == null) return Ok(ApiResponse<string>.Ok(string.Empty, message));

        var now = DateTime.UtcNow;
        var since = now.AddMinutes(-5);
        var reserved = await _db.Users.Where(x => x.Id == user.Id && x.EmailVerifiedAt == null &&
                (x.LastVerificationSentAt == null || x.LastVerificationSentAt <= since))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastVerificationSentAt, now));
        if (reserved != 1)
            return Ok(ApiResponse<string>.Ok(string.Empty, message));

        try
        {
            var rawToken = AuthTokenTools.NewToken();
            _db.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId = user.Id, TokenHash = AuthTokenTools.Hash(rawToken),
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await _db.SaveChangesAsync();
            await SendVerificationAsync(email, rawToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Could not complete verification resend: {ErrorType}", exception.GetType().Name);
        }
        return Ok(ApiResponse<string>.Ok(string.Empty, message));
    }

    private Task SendVerificationAsync(string email, string token)
    {
        var baseUrl = _config["Mail:PublicFrontendUrl"] ??
            (_environment.IsDevelopment() ? "https://localhost:5173" :
                throw new InvalidOperationException("Missing public frontend URL"));
        var url = $"{baseUrl.TrimEnd('/')}/auth/verify-email#token={Uri.EscapeDataString(token)}";
        return _email.SendAsync(email, url);
    }
}

public class RegisterStudentRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string FullName { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6), RegularExpression("^[a-zA-Z0-9_]+$")]
    public string UserName { get; set; } = "";
    [Required, EmailAddress, StringLength(450)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8), RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).+$")]
    public string Password { get; set; } = "";
    [RegularExpression(@"^[0-9]{10,11}$")] public string? PhoneNumber { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string Address { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    [Required] public bool? Sex { get; set; }
}

public class VerifyEmailRequest
{
    [Required] public string Token { get; set; } = "";
}

public class ResendVerificationRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
}
