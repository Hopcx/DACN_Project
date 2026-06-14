using Project.Application.DTOs.ClassDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IClassService
    {
        Task<List<ClassResponseDto>> GetAllAsync(string? textSearch);
        Task<ClassResponseDto?> GetByIdAsync(int id);
        Task<ClassResponseDto?> CreateAsync(ClassCreateDto dto);
        Task<ClassResponseDto?> UpdateAsync(int id, ClassCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
