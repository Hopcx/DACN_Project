using Project.Application.DTOs.ExamDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IExamService
    {
        Task<List<ExamResponseDto>> GetAllAsync(string? textSearch, bool? isActive);
        Task<ExamResponseDto?> GetByIdAsync(int id);
        Task<ExamResponseDto?> CreateAsync(ExamCreateDto dto);
        Task<ExamResponseDto?> UpdateAsync(int id, ExamCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
