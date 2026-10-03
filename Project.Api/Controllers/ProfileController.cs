using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Application.Common;
using Project.Application.DTOs.UserDTO;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;

namespace Project.Api.Controllers;

[ApiController]
[Authorize]
[Route("web/profile")]
public class ProfileController : ControllerBase
{
    private readonly ProjectDACNDbContext _db;

    public ProfileController(ProjectDACNDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!TryGetUserId(out var id)) return Unauthorized();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == 1);
        return user == null ? NotFound(ApiResponse<string>.Fail("Tài khoản không tồn tại hoặc đã bị khóa"))
            : Ok(ApiResponse<UserResponseDto>.Ok(UserAccountMapper.ToDto(user)));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ProfileUpdateRequest request)
    {
        if (!TryGetUserId(out var id)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Address))
            return BadRequest(ApiResponse<string>.Fail("Họ tên và địa chỉ không được để trống"));
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Status == 1);
        if (user == null) return NotFound(ApiResponse<string>.Fail("Tài khoản không tồn tại hoặc đã bị khóa"));
        user.FullName = request.FullName.Trim();
        user.Address = request.Address.Trim();
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<UserResponseDto>.Ok(UserAccountMapper.ToDto(user)));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!TryGetUserId(out var id)) return Unauthorized();
        if (request.NewPassword == request.OldPassword)
            return BadRequest(ApiResponse<string>.Fail("Mật khẩu mới phải khác mật khẩu cũ"));

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == 1);
        if (user == null) return NotFound(ApiResponse<string>.Fail("Tài khoản không tồn tại hoặc đã bị khóa"));
        bool oldPasswordValid;
        try { oldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash); }
        catch (Exception) { oldPasswordValid = false; } // Legacy/non-BCrypt hashes cannot verify.
        if (!oldPasswordValid)
            return BadRequest(ApiResponse<string>.Fail("Mật khẩu cũ không đúng"));

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var changed = await _db.Users.Where(x => x.Id == id && x.Status == 1 && x.PasswordHash == user.PasswordHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PasswordHash, newHash));
        if (changed != 1)
            return Conflict(ApiResponse<string>.Fail("Tài khoản đã thay đổi, vui lòng thử lại"));
        await _db.RefreshTokens.Where(x => x.UserId == id && !x.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRevoked, true));
        await transaction.CommitAsync();
        return Ok(ApiResponse<string>.Ok(string.Empty, "Đã đổi mật khẩu và thu hồi phiên làm mới"));
    }

    private bool TryGetUserId(out Guid id) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}

public class ProfileUpdateRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string FullName { get; set; } = "";
    [Required, StringLength(200, MinimumLength = 1)] public string Address { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required] public string OldPassword { get; set; } = "";
    [Required, MinLength(8), RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).+$")]
    public string NewPassword { get; set; } = "";
}

internal static class UserAccountMapper
{
    internal static UserResponseDto ToDto(User user) => new()
    {
        Id = user.Id, FullName = user.FullName, UserName = user.UserName,
        DateOfBirth = user.DateOfBirth, PhoneNumber = user.PhoneNumber,
        Address = user.Address, Email = user.Email, AvatarUrl = user.AvatarUrl,
        Sex = user.Sex, LastLogin = user.LastLogin, Status = user.Status, LevelId = user.LevelId
    };
}
