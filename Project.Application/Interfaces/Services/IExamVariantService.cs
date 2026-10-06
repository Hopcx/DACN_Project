using Project.Application.DTOs.ExamDetailDTO;

namespace Project.Application.Interfaces.Services;

public interface IExamVariantService
{
    Task<List<ExamVariantResponseDto>> GetByExamAsync(int examId);
    Task<ExamVariantResponseDto?> GetAsync(int examId, int id);
    Task<List<ExamQuestionOptionDto>> GetQuestionOptionsAsync(int examId);
    Task<List<ExamSubjectOptionDto>> GetSubjectsAsync();
    Task<ExamVariantResponseDto> SaveAsync(int examId, int? id, ExamVariantSaveDto dto, Guid actorId);
}
