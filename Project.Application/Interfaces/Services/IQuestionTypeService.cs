using Project.Application.DTOs.QuestionTypeDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IQuestionTypeService
    {
        Task<List<QuestionTypeResponseDto>> GetAllQuestionTypesAsync();
        Task<QuestionTypeResponseDto?> GetQuestionTypeByIdAsync(int id);
        Task<QuestionTypeResponseDto?> CreateQuestionTypeAsync(QuestionTypeCreateDto dto);
        Task<QuestionTypeResponseDto?> UpdateQuestionTypeAsync(int id, QuestionTypeCreateDto dto);
        Task<bool> DeleteQuestionTypeAsync(int id);
    }
}
