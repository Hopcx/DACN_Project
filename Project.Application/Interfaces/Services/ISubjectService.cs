using Project.Application.DTOs.SubjectDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface ISubjectService
    {
        Task<List<SubjectResponseDto>> GetAllSubjectsAsync(string? textSearch, bool? isActive);
        Task<SubjectResponseDto?> GetSubjectByIdAsync(int id);
        Task<SubjectResponseDto?> CreateSubjectAsync(SubjectCreateDto dto);
        Task<SubjectResponseDto?> UpdateSubjectAsync(int id, SubjectCreateDto dto);
        Task<bool> DeleteSubjectAsync(int id);
    }
}
