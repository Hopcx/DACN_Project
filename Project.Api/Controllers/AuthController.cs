using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.UserDTO;
using Project.Application.Interfaces.Services;
using Project.Application.Services;
using Project.Domain.Interfaces.Repositories;
using System.Security.Claims;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;

        public AuthController(IUserRepository userRepository, IJwtService jwtService)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Login endpoint - nhận username/email/phone và password, trả về JWT token
        /// </summary>
        [HttpPost("login")]
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

            // Generate JWT token
            var token = _jwtService.GenerateToken(user.Id, user.UserName, user.LevelId);

            return Ok(ApiResponse<LoginResponseDto>.Ok(new LoginResponseDto
            {
                Token = token,
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                LevelId = user.LevelId
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
            var levelId = User.FindFirstValue("levelId");

            return Ok(ApiResponse<object>.Ok(new
            {
                UserId = userId,
                UserName = userName,
                LevelId = levelId
            }));
        }
    }

    public class LoginDto
    {
        public string Keyword { get; set; } = null!; // Username, Email, or PhoneNumber
        public string Password { get; set; } = null!;
    }

    public class LoginResponseDto
    {
        public string Token { get; set; } = null!;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int LevelId { get; set; }
    }
}
