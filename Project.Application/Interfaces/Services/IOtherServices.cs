using Project.Application.DTOs.ClassExamScheduleDTO;
using Project.Application.DTOs.ClassUserDTO;
using Project.Application.DTOs.ExamDetailDTO;
using Project.Application.DTOs.ExamDetailQuestionDTO;
using Project.Application.DTOs.ExamActivityLogDTO;
using Project.Application.DTOs.LogDTO;
using Project.Application.DTOs.UserPermissionDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Application.Interfaces.Services
{
    public interface IClassExamScheduleService { Task<List<ClassExamScheduleResponseDto>> GetAllAsync(); Task<ClassExamScheduleResponseDto?> GetByIdAsync(int id); Task<ClassExamScheduleResponseDto?> CreateAsync(ClassExamScheduleCreateDto dto); Task<ClassExamScheduleResponseDto?> UpdateAsync(int id, ClassExamScheduleCreateDto dto); Task<bool> DeleteAsync(int id); }
    public interface IClassUserService { Task<List<ClassUserResponseDto>> GetAllAsync(); Task<ClassUserResponseDto?> GetByIdAsync(int id); Task<ClassUserResponseDto?> CreateAsync(ClassUserCreateDto dto); Task<ClassUserResponseDto?> UpdateAsync(int id, ClassUserCreateDto dto); Task<bool> DeleteAsync(int id); }
    public interface IExamDetailService { Task<List<ExamDetailResponseDto>> GetAllAsync(); Task<ExamDetailResponseDto?> GetByIdAsync(int id); Task<ExamDetailResponseDto?> CreateAsync(ExamDetailCreateDto dto); Task<ExamDetailResponseDto?> UpdateAsync(int id, ExamDetailCreateDto dto); Task<bool> DeleteAsync(int id); }
    public interface IExamDetailQuestionService { Task<List<ExamDetailQuestionResponseDto>> GetAllAsync(); Task<ExamDetailQuestionResponseDto?> GetByIdAsync(int id); Task<ExamDetailQuestionResponseDto?> CreateAsync(ExamDetailQuestionCreateDto dto); Task<ExamDetailQuestionResponseDto?> UpdateAsync(int id, ExamDetailQuestionCreateDto dto); Task<bool> DeleteAsync(int id); }
    public interface IExamActivityLogService { Task<List<ExamActivityLogResponseDto>> GetAllAsync(); Task<ExamActivityLogResponseDto?> GetByIdAsync(int id); Task<ExamActivityLogResponseDto?> CreateAsync(ExamActivityLogCreateDto dto); Task<ExamActivityLogResponseDto?> UpdateAsync(int id, ExamActivityLogCreateDto dto); Task<bool> DeleteAsync(int id); }
    public interface ILogService { Task<List<LogResponseDto>> GetAllAsync(); Task<LogResponseDto?> GetByIdAsync(int id); Task<LogResponseDto?> CreateAsync(LogCreateDto dto); Task<LogResponseDto?> UpdateAsync(int id, LogCreateDto dto); Task<bool> DeleteAsync(int id); }
    public interface IUserPermissionService { Task<List<UserPermissionResponseDto>> GetAllAsync(); Task<UserPermissionResponseDto?> GetByIdAsync(int id); Task<UserPermissionResponseDto?> CreateAsync(UserPermissionCreateDto dto); Task<UserPermissionResponseDto?> UpdateAsync(int id, UserPermissionCreateDto dto); Task<bool> DeleteAsync(int id); }
}
