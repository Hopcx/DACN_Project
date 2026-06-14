using Project.Application.DTOs.AnswerSubmissionDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IAnswerSubmissionService
    {
        Task<List<AnswerSubmissionResponseDto>> GetAllAsync();
        Task<AnswerSubmissionResponseDto?> GetByIdAsync(int id);
        Task<AnswerSubmissionResponseDto?> CreateAsync(AnswerSubmissionCreateDto dto);
        Task<AnswerSubmissionResponseDto?> UpdateAsync(int id, AnswerSubmissionCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
