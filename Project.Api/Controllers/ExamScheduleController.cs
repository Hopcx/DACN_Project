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
        private readonly ProjectDACNDbContext _db;
        private readonly ScheduleWriteGuard _guard;

        public ExamScheduleController(ProjectDACNDbContext db, ScheduleWriteGuard guard)
        {
            _db = db;
            _guard = guard;
        }

        [HttpGet("exam-options")]
        public async Task<IActionResult> GetExamOptions() => Ok(ApiResponse<object>.Ok(
            await _db.Exams.AsNoTracking().Where(x => x.Status != 255)
                .OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Name, x.SubjectId, x.Status }).ToListAsync()));

        [HttpGet("room-options")]
        public async Task<IActionResult> GetRoomOptions() => Ok(ApiResponse<object>.Ok(
            await _db.Rooms.AsNoTracking().Where(x => x.Status != false)
                .OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Name, x.Capacity }).ToListAsync()));

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
            var rows = await _db.ExamSchedules.AsNoTracking()
                .OrderBy(x => x.StartTime).ThenBy(x => x.Id)
                .Select(x => new { Schedule = x, HasAttempts =
                    _db.DoingExams.Any(d => d.ExamScheduleId == x.Id) ||
                    _db.Submissions.Any(s => s.ExamScheduleId == x.Id) })
                .ToListAsync();
            var result = rows.Select(x => ToDto(x.Schedule, x.HasAttempts)).ToList();
            return Ok(ApiResponse<List<ExamScheduleResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetExamScheduleById(int id)
        {
            var row = await _db.ExamSchedules.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { Schedule = x, HasAttempts =
                    _db.DoingExams.Any(d => d.ExamScheduleId == x.Id) ||
                    _db.Submissions.Any(s => s.ExamScheduleId == x.Id) })
                .FirstOrDefaultAsync();
            if (row == null)
                return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));

            return Ok(ApiResponse<ExamScheduleResponseDto>.Ok(ToDto(row.Schedule, row.HasAttempts)));
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
                CreatedAt = DateTime.UtcNow, IsTimeUtc = true };
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
            if (!schedule.IsTimeUtc)
                return Conflict(ApiResponse<string>.Fail("Lịch cũ chưa xác định múi giờ; hãy tạo lịch mới"));
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
            return Ok(ApiResponse<ExamScheduleResponseDto>.Ok(ToDto(schedule,
                await _guard.HasAttemptsAsync(id)), "Cập nhật Exam schedule thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExamSchedule(int id)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await _guard.AcquireAsync();
            var schedule = await _db.ExamSchedules.FindAsync(id);
            if (schedule == null) return NotFound(ApiResponse<string>.Fail("Exam schedule không tồn tại"));
            if (!schedule.IsTimeUtc)
                return Conflict(ApiResponse<string>.Fail("Lịch cũ chưa xác định múi giờ; hãy tạo lịch mới"));
            if (await _guard.HasAttemptsAsync(id))
                return Conflict(ApiResponse<string>.Fail("Lịch đã có lượt thi hoặc bài nộp"));
            schedule.Status = 255;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(ApiResponse<string>.Ok($"Xóa Exam schedule với ID {id} thành công"));
        }

        private static ExamScheduleResponseDto ToDto(ExamSchedule x, bool hasAttempts = false) => new()
        { Id = x.Id, ExamId = x.ExamId, Title = x.Title,
          StartTime = x.IsTimeUtc ? DateTime.SpecifyKind(x.StartTime, DateTimeKind.Utc) : x.StartTime,
          EndTime = x.IsTimeUtc ? DateTime.SpecifyKind(x.EndTime, DateTimeKind.Utc) : x.EndTime,
          TimeZoneStatus = x.IsTimeUtc ? "utc" : "unknown", HasAttempts = hasAttempts,
          Description = x.Description, Status = x.Status, SubjectId = x.SubjectId, RoomId = x.RoomId };
    }

    public record ScheduleClassOptionDto(int Id, string Name, string ClassCode);
}
