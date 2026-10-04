using Microsoft.AspNetCore.Mvc;
using Project.Application.Common;
using Project.Application.DTOs.ClassDTO;
using Project.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Project.Infrastructure.Persistence;
using System.Data;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("web/classes")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "AdminManagement")]
    public class ClassController : ControllerBase
    {
        private readonly IClassService _service;
        private readonly ProjectDACNDbContext _db;

        public ClassController(IClassService service, ProjectDACNDbContext db)
        {
            _service = service;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? textSearch)
        {
            var result = await _service.GetAllAsync(textSearch);
            return Ok(ApiResponse<List<ClassResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Class không tồn tại"));

            return Ok(ApiResponse<ClassResponseDto>.Ok(result));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClassCreateDto dto)
        {
            var error = await ValidateAsync(dto);
            if (error != null) return error;
            if (await _db.Classes.AnyAsync(x => x.ClassCode == dto.ClassCode))
                return Conflict(ApiResponse<string>.Fail("Mã lớp đã được sử dụng"));
            ClassResponseDto? result;
            try { result = await _service.CreateAsync(dto); }
            catch (DbUpdateException ex) when (IsUnique(ex))
            { return Conflict(ApiResponse<string>.Fail("Mã lớp đã được sử dụng")); }
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail("Tạo Class thất bại"));

            return Created("", ApiResponse<ClassResponseDto>.Ok(result, "Tạo Class thành công"));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClassCreateDto dto)
        {
            var error = await ValidateAsync(dto);
            if (error != null) return error;
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var schoolClass = await _db.Classes.FromSqlInterpolated(
                $"SELECT * FROM [Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").FirstOrDefaultAsync();
            if (schoolClass == null) return NotFound(ApiResponse<string>.Fail("Class không tồn tại"));
            var enrolled = await _db.ClassUsers.CountAsync(x => x.ClassId == id && x.Status == 1);
            if (dto.Capacity < enrolled)
                return Conflict(ApiResponse<string>.Fail("Sĩ số mới nhỏ hơn số học viên đã duyệt"));
            if (await _db.Classes.AnyAsync(x => x.Id != id && x.ClassCode == dto.ClassCode))
                return Conflict(ApiResponse<string>.Fail("Mã lớp đã được sử dụng"));
            ClassResponseDto? result;
            try { result = await _service.UpdateAsync(id, dto); }
            catch (DbUpdateException ex) when (IsUnique(ex))
            { return Conflict(ApiResponse<string>.Fail("Mã lớp đã được sử dụng")); }
            await transaction.CommitAsync();
            if (result == null)
                return NotFound(ApiResponse<string>.Fail("Class không tồn tại"));

            return Ok(ApiResponse<ClassResponseDto>.Ok(result, "Cập nhật Class thành công"));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            _ = await _db.Classes.FromSqlInterpolated(
                $"SELECT * FROM [Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").FirstOrDefaultAsync();
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(ApiResponse<string>.Fail("Class không tồn tại hoặc xóa thất bại"));
            await transaction.CommitAsync();

            return Ok(ApiResponse<string>.Ok($"Xóa Class với ID {id} thành công"));
        }

        [HttpGet("options")]
        public async Task<IActionResult> Options()
        {
            var teachers = await _db.Users.AsNoTracking().Where(x => x.LevelId == 3 && x.Status == 1)
                .OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName }).ToListAsync();
            var subjects = await _db.Subjects.AsNoTracking().Where(x => x.Status == 1)
                .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync();
            return Ok(ApiResponse<object>.Ok(new { teachers, subjects }));
        }

        private async Task<IActionResult?> ValidateAsync(ClassCreateDto dto)
        {
            dto.Name = dto.Name.Trim();
            dto.ClassCode = dto.ClassCode.Trim();
            if (dto.Name.Length == 0 || dto.ClassCode.Length == 0)
                return BadRequest(ApiResponse<string>.Fail("Tên và mã lớp không được trống"));
            if (dto.Status != 0 && dto.Status != 1)
                return BadRequest(ApiResponse<string>.Fail("Trạng thái lớp phải là hoạt động hoặc tạm ngừng"));
            if (!await _db.Users.AnyAsync(x => x.Id == dto.TeacherId && x.LevelId == 3 && x.Status == 1))
                return BadRequest(ApiResponse<string>.Fail("Giảng viên không hợp lệ"));
            if (dto.SubjectId != null && !await _db.Subjects.AnyAsync(x => x.Id == dto.SubjectId && x.Status == 1))
                return BadRequest(ApiResponse<string>.Fail("Môn học không hoạt động hoặc không tồn tại"));
            return null;
        }

        private static bool IsUnique(DbUpdateException ex) => ex.InnerException is SqlException sql &&
            (sql.Number == 2601 || sql.Number == 2627);
    }
}
