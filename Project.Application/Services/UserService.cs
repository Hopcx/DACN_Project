using Project.Application.DTOs.RoomDTO;
using Project.Application.DTOs.UserDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<UserResponseDto> CreateRoomAsync(UserCreateDto dto)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = dto.FullName,
                UserName = dto.UserName,
                DateOfBirth = dto.DateOfBirth,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                Email = dto.Email,
                PasswordHash = dto.PasswordHash,
                AvatarUrl = dto.AvatarUrl,
                Sex = dto.Sex,
                LastLogin = dto.LastLogin,
                LevelId = dto.LevelId,
                Status = dto.Status 
            };

            var createUser = await _userRepository.AddUserAsync(user);
            if (createUser != null)
                return null;
            return new UserResponseDto
            {
                Id = createUser.Id,
                FullName = createUser.FullName,
                UserName = createUser.UserName,
                DateOfBirth = createUser.DateOfBirth,
                PhoneNumber = createUser.PhoneNumber,
                Address = createUser.Address,
                Email = createUser.Email,
                PasswordHash = createUser.PasswordHash,
                AvatarUrl = createUser.AvatarUrl,
                Sex = createUser.Sex,
                LastLogin = createUser.LastLogin,
                LevelId = createUser.LevelId,
                Status = createUser.Status
            };
        }

        public async Task<bool> DeleteUsserAsync(string id)
        {
            var deletedUser = await _userRepository.DeleteUserAsync(Guid.Parse(id));
            return deletedUser != null;
        }

        public async Task<List<UserResponseDto>> GetAllUsserAsync()
        {
           var user = await _userRepository.GetAllUsersAsync();
            return user.Select(r => new UserResponseDto
            {
                Id = r.Id,
                FullName = r.FullName,
                UserName = r.UserName,
                DateOfBirth = r.DateOfBirth,
                PhoneNumber = r.PhoneNumber,
                Address = r.Address,
                Email = r.Email,
                PasswordHash = r.PasswordHash,
                AvatarUrl = r.AvatarUrl,
                Sex = r.Sex,
                LastLogin = r.LastLogin,
                LevelId = r.LevelId,
                Status = r.Status
            }).ToList();
        }
    }
}
