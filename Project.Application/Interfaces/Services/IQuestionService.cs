using Project.Application.DTOs.QuestionDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IQuestionService
    {
        Task<List<QuestionResponseDto>> GetAllAsync(string? textSearch);
        Task<QuestionResponseDto?> GetByIdAsync(int id);
        Task<QuestionResponseDto?> CreateAsync(QuestionCreateDto dto);
        Task<QuestionResponseDto?> UpdateAsync(int id, QuestionCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
