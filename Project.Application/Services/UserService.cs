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
using BCrypt.Net;
using AutoMapper;
using Project.Application.Exceptions;

namespace Project.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public UserService(IUserRepository userRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<UserResponseDto> CreateUserAsync(UserCreateDto dto)
        {
            // Hash password using BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // ===== CÁCH CŨ: Manual mapping (đã comment) =====
            //var user = new User
            //{
            //    Id = Guid.NewGuid(),
            //    FullName = dto.FullName,
            //    UserName = dto.UserName,
            //    DateOfBirth = dto.DateOfBirth,
            //    PhoneNumber = dto.PhoneNumber,
            //    Address = dto.Address,
            //    Email = dto.Email,
            //    PasswordHash = passwordHash,
            //    AvatarUrl = dto.AvatarUrl,
            //    Sex = dto.Sex,
            //    LastLogin = dto.LastLogin,
            //    LevelId = dto.LevelId,
            //    Status = dto.Status 
            //};

            // ===== CÁCH MỚI: Sử dụng AutoMapper =====
            var user = _mapper.Map<User>(dto);
            user.Id = Guid.NewGuid();
            user.PasswordHash = passwordHash; // Set password hash sau khi map

            var createUser = await _userRepository.AddUserAsync(user);
            if (createUser == null)
                throw new BadRequestException("Tạo User thất bại");

            // ===== CÁCH CŨ: Manual mapping (đã comment) =====
            //return new UserResponseDto
            //{
            //    Id = createUser.Id,
            //    FullName = createUser.FullName,
            //    UserName = createUser.UserName,
            //    DateOfBirth = createUser.DateOfBirth,
            //    PhoneNumber = createUser.PhoneNumber,
            //    Address = createUser.Address,
            //    Email = createUser.Email,
            //    PasswordHash = createUser.PasswordHash,
            //    AvatarUrl = createUser.AvatarUrl,
            //    Sex = createUser.Sex,
            //    LastLogin = createUser.LastLogin,
            //    LevelId = createUser.LevelId,
            //    Status = createUser.Status
            //};

            // ===== CÁCH MỚI: Sử dụng AutoMapper =====
            return _mapper.Map<UserResponseDto>(createUser);
        }

        public async Task<bool> DeleteUsserAsync(string id)
        {
            var deletedUser = await _userRepository.DeleteUserAsync(Guid.Parse(id));
            if (deletedUser == null)
                throw new NotFoundException("User", id);
            return true;
        }

        public async Task<List<UserResponseDto>> GetAllUsserAsync()
        {
            var users = await _userRepository.GetAllUsersAsync();

            // ===== CÁCH CŨ: Manual mapping với LINQ Select (đã comment) =====
            //return user.Select(r => new UserResponseDto
            //{
            //    Id = r.Id,
            //    FullName = r.FullName,
            //    UserName = r.UserName,
            //    DateOfBirth = r.DateOfBirth,
            //    PhoneNumber = r.PhoneNumber,
            //    Address = r.Address,
            //    Email = r.Email,
            //    PasswordHash = r.PasswordHash,
            //    AvatarUrl = r.AvatarUrl,
            //    Sex = r.Sex,
            //    LastLogin = r.LastLogin,
            //    LevelId = r.LevelId,
            //    Status = r.Status
            //}).ToList();

            // ===== CÁCH MỚI: Sử dụng AutoMapper =====
            return _mapper.Map<List<UserResponseDto>>(users);
        }
    }
}
