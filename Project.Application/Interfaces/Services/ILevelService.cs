using Project.Application.DTOs.LevelDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface ILevelService
    {
        Task<List<LevelResponseDto>> GetAllLevelAsync();
        Task<LevelResponseDto> CreateLevelAsync(LevelCreateDto dto);
        Task<LevelResponseDto> UpdateLevelAsync(LevelUpdateDto dto);
        Task<bool> DeleteLevelAsync(int id);

        Task<List<UserLevelResponseDto>> GetUsersByLevelAsync(int levelId, string? textSearch);
    }

}
