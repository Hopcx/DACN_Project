using Project.Application.DTOs.LevelDTO;
using Project.Application.DTOs.PermissionDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IPermissionService
    {
        Task<List<PermissionResponseDto>> GetAllPermissionAsync();
        Task<PermissionResponseDto> CreatePermissionsAsync(PermissionCreateDto dto);
        Task<PermissionResponseDto> UpdatePermissionsAsync(int id,PermissionCreateDto dto);
        Task<bool> DeletePermissionAsync(int id);
    }
}
