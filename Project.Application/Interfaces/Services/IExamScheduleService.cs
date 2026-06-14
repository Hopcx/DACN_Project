using Project.Application.DTOs.ExamScheduleDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IExamScheduleService
    {
        Task<List<ExamScheduleResponseDto>> GetAllExamSchedulesAsync();
        Task<ExamScheduleResponseDto?> GetExamScheduleByIdAsync(int id);
        Task<ExamScheduleResponseDto?> CreateExamScheduleAsync(ExamScheduleCreateDto dto);
        Task<ExamScheduleResponseDto?> UpdateExamScheduleAsync(int id, ExamScheduleCreateDto dto);
        Task<bool> DeleteExamScheduleAsync(int id);
    }
}
