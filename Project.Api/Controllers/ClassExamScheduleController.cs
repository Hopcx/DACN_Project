using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ClassExamScheduleDTO;
using Project.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;
using Project.Domain.Entities;
using System.Data;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/class-exam-schedules")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ScheduleManagement")]
    public class ClassExamScheduleController : ControllerBase
    {
        private readonly IClassExamScheduleService _service;
        private readonly ProjectDACNDbContext _db;
        private readonly ScheduleWriteGuard _guard;

        public ClassExamScheduleController(IClassExamScheduleService service, ProjectDACNDbContext db, ScheduleWriteGuard guard)
        {
            _service = service;
            _db = db;
            _guard = guard;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse<List<ClassExamScheduleResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Class exam schedule không tồn tại"));

            return Ok(ApiResponse<ClassExamScheduleResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClassExamScheduleCreateDto dto)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var invalid = await ValidateAssignment(dto, null);
            if (invalid != null) return invalid;
            var link = new ClassExamSchedule { ClassId = dto.ClassId, ExamScheduleId = dto.ExamScheduleId };
            _db.ClassExamSchedules.Add(link);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Created("", ApiResponse<ClassExamScheduleResponseDto>.Ok(ToDto(link), "Gán lớp thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassExamScheduleCreateDto dto)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var link = await _db.ClassExamSchedules.FindAsync(id);
            if (link == null)
                return NotFound(ApiResponse<string>.Fail("Class exam schedule không tồn tại"));
            if ((link.ClassId != dto.ClassId || link.ExamScheduleId != dto.ExamScheduleId) &&
                await _guard.HasAttemptsAsync(link.ExamScheduleId))
                return Conflict(ApiResponse<string>.Fail("Lịch cũ đã có lượt thi hoặc bài nộp"));
            var invalid = await ValidateAssignment(dto, id);
            if (invalid != null) return invalid;
            link.ClassId = dto.ClassId; link.ExamScheduleId = dto.ExamScheduleId;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ApiResponse<ClassExamScheduleResponseDto>.Ok(ToDto(link), "Cập nhật gán lớp thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var link = await _db.ClassExamSchedules.FindAsync(id);
            if (link == null) return NotFound(ApiResponse<string>.Fail("Class exam schedule không tồn tại"));
            if (await _guard.HasAttemptsAsync(link.ExamScheduleId))
                return Conflict(ApiResponse<string>.Fail("Lịch đã có lượt thi hoặc bài nộp"));
            _db.ClassExamSchedules.Remove(link);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ApiResponse<string>.Ok($"Xóa Class exam schedule với ID {id} thành công"));
        }

        private async Task<IActionResult?> ValidateAssignment(ClassExamScheduleCreateDto dto, int? excludedId)
        {
            var schedule = await _db.ExamSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.ExamScheduleId);
            if (schedule == null || schedule.Status == 255)
                return NotFound(ApiResponse<string>.Fail("Lịch thi không tồn tại"));
            if (!await _db.Classes.AnyAsync(x => x.Id == dto.ClassId && x.Status == 1))
                return BadRequest(ApiResponse<string>.Fail("Lớp không hoạt động hoặc không tồn tại"));
            if (await _guard.HasAttemptsAsync(schedule.Id))
                return Conflict(ApiResponse<string>.Fail("Lịch đã có lượt thi hoặc bài nộp"));
            if (await _db.ClassExamSchedules.AnyAsync(x => x.Id != excludedId &&
                x.ClassId == dto.ClassId && x.ExamScheduleId == dto.ExamScheduleId))
                return Conflict(ApiResponse<string>.Fail("Lớp đã được gán vào lịch này"));
            if (schedule.Status == 1 && await _guard.StudentConflictAsync(schedule.Id,
                schedule.StartTime, schedule.EndTime, new[] { dto.ClassId }))
                return Conflict(ApiResponse<string>.Fail("Học viên đã được duyệt có lịch thi giao nhau"));
            return null;
        }

        private static ClassExamScheduleResponseDto ToDto(ClassExamSchedule x) => new()
        { Id = x.Id, ClassId = x.ClassId, ExamScheduleId = x.ExamScheduleId };
    }
}
