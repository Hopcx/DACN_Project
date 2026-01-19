using AutoMapper;
using Project.Application.DTOs.UserDTO;
using Project.Domain.Entities;

namespace Project.Application.Mappings
{
    /// <summary>
    /// AutoMapper profile để map giữa Entity và DTO
    /// Thay thế cho manual mapping
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User mappings
            CreateMap<User, UserResponseDto>();
            CreateMap<UserCreateDto, User>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) // Will be set manually after hashing
                .ForMember(dest => dest.ClassUsers, opt => opt.Ignore())
                .ForMember(dest => dest.ExamActivityLogs, opt => opt.Ignore())
                .ForMember(dest => dest.Level, opt => opt.Ignore())
                .ForMember(dest => dest.Submissions, opt => opt.Ignore())
                .ForMember(dest => dest.UserPermissions, opt => opt.Ignore());
        }
    }
}
