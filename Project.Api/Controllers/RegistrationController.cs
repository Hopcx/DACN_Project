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
        if (string.IsNullOrWhiteSpace(request.FullName)) return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập họ và tên."));
        if (string.IsNullOrWhiteSpace(request.Address)) return BadRequest(ApiResponse<string>.Fail("Vui lòng nhập địa chỉ."));
        if (userName.Length < 6) return BadRequest(ApiResponse<string>.Fail("Tên đăng nhập phải có từ 6 đến 100 ký tự."));
        if (request.DateOfBirth == default) return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn ngày sinh."));
        if (request.DateOfBirth.Date > DateTime.UtcNow.Date.AddDays(1))
            return BadRequest(ApiResponse<string>.Fail("Ngày sinh không được ở tương lai."));
        if (request.DateOfBirth.Date < new DateTime(1900, 1, 1))
            return BadRequest(ApiResponse<string>.Fail("Ngày sinh không hợp lệ."));
        if (await _db.Users.AnyAsync(x => x.Email.ToLower() == email || x.UserName == userName ||
                (phone != null && x.PhoneNumber == phone)))
            return Conflict(ApiResponse<string>.Fail("Email, tên đăng nhập hoặc số điện thoại đã được sử dụng. Vui lòng kiểm tra lại hoặc đăng nhập."));

        var studentLevelId = await _db.Levels.Where(x => x.Name == "Student" && x.Status == 1)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (studentLevelId == null)
            return StatusCode(503, ApiResponse<string>.Fail("Hệ thống chưa được cấu hình vai trò học viên. Vui lòng liên hệ quản trị viên."));

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
                return Conflict(ApiResponse<string>.Fail("Email, tên đăng nhập hoặc số điện thoại đã được sử dụng. Vui lòng kiểm tra lại hoặc đăng nhập."));
            }
            await transaction.CommitAsync();
        }
        await SendVerificationAsync(email, rawToken);
        return Accepted(ApiResponse<string>.Ok(string.Empty, "Đăng ký thành công. Vui lòng kiểm tra email để xác minh tài khoản trước khi đăng nhập."));
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("auth-public")]
    public async Task<IActionResult> Verify([FromBody] VerifyEmailRequest request)
    {
        var hash = AuthTokenTools.Hash(request.Token);
        var token = await _db.EmailVerificationTokens.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (token == null || token.ConsumedAt != null || token.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(ApiResponse<string>.Fail("Liên kết xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu gửi lại email xác minh."));

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var updated = await _db.EmailVerificationTokens.Where(x => x.Id == token.Id &&
                x.ConsumedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, DateTime.UtcNow));
        if (updated != 1)
            return BadRequest(ApiResponse<string>.Fail("Liên kết xác minh đã được sử dụng."));
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
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Tên đăng nhập phải có từ 6 đến 100 ký tự.")]
    [RegularExpression("^[a-zA-Z0-9_]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ cái không dấu, số và dấu gạch dưới.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng (ví dụ: ten@example.com).")]
    [StringLength(450, ErrorMessage = "Email quá dài.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có từ 8 đến 128 ký tự.")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải có ít nhất một chữ hoa, một chữ thường và một chữ số.")]
    public string Password { get; set; } = "";

    [RegularExpression(@"^[0-9]{10,11}$", ErrorMessage = "Số điện thoại phải gồm 10 hoặc 11 chữ số.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Địa chỉ không được vượt quá 200 ký tự.")]
    public string Address { get; set; } = "";

    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giới tính.")]
    public bool? Sex { get; set; }
}

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "Thiếu mã xác minh email.")] public string Token { get; set; } = "";
}

public class ResendVerificationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không đúng định dạng.")] public string Email { get; set; } = "";
}
