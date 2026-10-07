using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ExamScheduleDTO;
using Project.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;
using Project.Domain.Entities;
using System.Data;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/exam-schedules")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "ScheduleManagement")]
    public class ExamScheduleController : ControllerBase
    {
        private readonly IExamScheduleService _service;
        private readonly ProjectDACNDbContext _db;
        private readonly ScheduleWriteGuard _guard;

        public ExamScheduleController(IExamScheduleService service, ProjectDACNDbContext db, ScheduleWriteGuard guard)
        {
            _service = service;
            _db = db;
            _guard = guard;
        }

        [HttpGet("class-options")]
        public async Task<IActionResult> GetClassOptions()
        {
            var rows = await _db.Classes.AsNoTracking()
                .Where(x => x.Status == 1)
                .OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Select(x => new ScheduleClassOptionDto(x.Id, x.Name, x.ClassCode))
                .ToListAsync();
            return Ok(ApiResponse<List<ScheduleClassOptionDto>>.Ok(rows));
        }

        private async Task<IActionResult?> ValidateSchedule(ExamScheduleCreateDto dto)
        {
            if (dto.StartTime.Kind != DateTimeKind.Utc || dto.EndTime.Kind != DateTimeKind.Utc ||
                dto.StartTime == default || dto.EndTime == default || dto.StartTime >= dto.EndTime)
                return BadRequest(ApiResponse<string>.Fail("startTime và endTime phải là UTC có hậu tố Z; thời gian kết thúc phải sau bắt đầu"));
            if (dto.ExamId <= 0 || !await _db.Exams.AnyAsync(x => x.Id == dto.ExamId && x.Status != 255))
                return BadRequest(ApiResponse<string>.Fail("Bài thi không tồn tại hoặc đã ngừng hoạt động"));
            if (dto.RoomId.HasValue && !await _db.Rooms.AnyAsync(x => x.Id == dto.RoomId.Value))
                return BadRequest(ApiResponse<string>.Fail("Phòng thi không tồn tại"));
            if (dto.SubjectId.HasValue && !await _db.Subjects.AnyAsync(x => x.Id == dto.SubjectId.Value))
                return BadRequest(ApiResponse<string>.Fail("Môn học không tồn tại"));
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllExamSchedules()
        {
            var result = await _service.GetAllExamSchedulesAsync();
            return Ok(ApiResponse<List<ExamScheduleResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetExamScheduleById(int id)
        {
            var result = await _service.GetExamScheduleByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));

            return Ok(ApiResponse<ExamScheduleResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> CreateExamSchedule([FromBody] ExamScheduleCreateDto dto)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var invalid = await ValidateSchedule(dto);
            if (invalid != null) return invalid;
            if (dto.Status == 1 && await _guard.RoomConflictAsync(dto.RoomId, null, dto.StartTime, dto.EndTime))
                return Conflict(ApiResponse<string>.Fail("Phòng đã có lịch thi giao nhau"));
            var schedule = new ExamSchedule { ExamId = dto.ExamId, Title = dto.Title,
                StartTime = dto.StartTime, EndTime = dto.EndTime, Description = dto.Description,
                Status = dto.Status, SubjectId = dto.SubjectId, RoomId = dto.RoomId,
                CreatedAt = DateTime.UtcNow };
            _db.ExamSchedules.Add(schedule);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Created("", ApiResponse<ExamScheduleResponseDto>.Ok(ToDto(schedule), "Tạo Exam schedule thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExamSchedule(int id, [FromBody] ExamScheduleCreateDto dto)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var invalid = await ValidateSchedule(dto);
            if (invalid != null) return invalid;
            var schedule = await _db.ExamSchedules.FindAsync(id);
            if (schedule == null)
                return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));
            if (await _guard.HasAttemptsAsync(id) &&
                (schedule.StartTime != dto.StartTime || schedule.EndTime != dto.EndTime ||
                 schedule.ExamId != dto.ExamId || schedule.RoomId != dto.RoomId || dto.Status == 255))
                return Conflict(ApiResponse<string>.Fail("Lịch đã có lượt thi hoặc bài nộp; hãy tạo lịch mới"));
            var classIds = await _db.ClassExamSchedules.Where(x => x.ExamScheduleId == id)
                .Select(x => x.ClassId).ToListAsync();
            if (dto.Status == 1 && await _guard.RoomConflictAsync(dto.RoomId, id, dto.StartTime, dto.EndTime))
                return Conflict(ApiResponse<string>.Fail("Phòng đã có lịch thi giao nhau"));
            if (dto.Status == 1 && await _guard.StudentConflictAsync(id, dto.StartTime, dto.EndTime, classIds))
                return Conflict(ApiResponse<string>.Fail("Học viên đã được duyệt có lịch thi giao nhau"));
            schedule.ExamId = dto.ExamId; schedule.Title = dto.Title;
            schedule.StartTime = dto.StartTime; schedule.EndTime = dto.EndTime;
            schedule.Description = dto.Description; schedule.Status = dto.Status;
            schedule.SubjectId = dto.SubjectId; schedule.RoomId = dto.RoomId;
            schedule.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ApiResponse<ExamScheduleResponseDto>.Ok(ToDto(schedule), "Cập nhật Exam schedule thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExamSchedule(int id)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var schedule = await _db.ExamSchedules.FindAsync(id);
            if (schedule == null) return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));
            if (await _guard.HasAttemptsAsync(id))
                return Conflict(ApiResponse<string>.Fail("Lịch đã có lượt thi hoặc bài nộp"));
            schedule.Status = 255;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ApiResponse<string>.Ok($"Xóa Exam schedule với ID {id} thành công"));
        }

        private static ExamScheduleResponseDto ToDto(ExamSchedule x) => new()
        { Id = x.Id, ExamId = x.ExamId, Title = x.Title, StartTime = x.StartTime,
          EndTime = x.EndTime, Description = x.Description, Status = x.Status,
          SubjectId = x.SubjectId, RoomId = x.RoomId };
    }

    public record ScheduleClassOptionDto(int Id, string Name, string ClassCode);
}
