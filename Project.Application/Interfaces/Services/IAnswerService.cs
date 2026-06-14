using Project.Application.DTOs.AnswerCreateDto;
using Project.Application.DTOs.LevelDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IAnswerService
    {
        Task<List<AnswerResponseDto>> GetAllAnswersAsync();
        Task<AnswerResponseDto> CreateAnswerAsync(AnswerCreateDto dto);
        Task<AnswerResponseDto> UpdateAnswerAsync(int id, AnswerCreateDto dto);

        Task<bool> DeleteAnswerAsync(int id);
    }
}
