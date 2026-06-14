using Project.Application.DTOs.QuestionLevelDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IQuestionLevelService
    {
        Task<List<QuestionLevelResponseDto>> GetAllQuestionLevelsAsync(string? textSearch);
        Task<QuestionLevelResponseDto?> GetQuestionLevelByIdAsync(int id);
        Task<QuestionLevelResponseDto?> CreateQuestionLevelAsync(QuestionLevelCreateDto dto);
        Task<QuestionLevelResponseDto?> UpdateQuestionLevelAsync(int id, QuestionLevelCreateDto dto);
        Task<bool> DeleteQuestionLevelAsync(int id);
    }
}
