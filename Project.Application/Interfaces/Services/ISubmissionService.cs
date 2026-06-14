using Project.Application.DTOs.SubmissionDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface ISubmissionService
    {
        Task<List<SubmissionResponseDto>> GetAllAsync();
        Task<SubmissionResponseDto?> GetByIdAsync(int id);
        Task<SubmissionResponseDto?> CreateAsync(SubmissionCreateDto dto);
        Task<SubmissionResponseDto?> UpdateAsync(int id, SubmissionCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
