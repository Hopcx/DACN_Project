using Project.Application.DTOs.ExamDetailDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class ExamDetailService : IExamDetailService
    {
        private readonly IExamDetailRepository _repository;

        public ExamDetailService(IExamDetailRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ExamDetailResponseDto>> GetAllAsync()
        {
            var items = await _repository.GetAllExamDetailsAsync();
            return items.Select(x => new ExamDetailResponseDto
            {
                Id = x.Id,
                ExamId = x.ExamId,
                Code = x.Code,
                Status = x.Status,
                CreateDate = x.CreateDate,
                CreateBy = x.CreateBy,
                UpdateDate = x.UpdateDate,
                UpdateBy = x.UpdateBy
            }).ToList();
        }

        public async Task<ExamDetailResponseDto?> GetByIdAsync(int id)
        {
            var item = await _repository.GetExamDetailByIdAsync(id);
            if (item == null)
                return null;

            return new ExamDetailResponseDto
            {
                Id = item.Id,
                ExamId = item.ExamId,
                Code = item.Code,
                Status = item.Status,
                CreateDate = item.CreateDate,
                CreateBy = item.CreateBy,
                UpdateDate = item.UpdateDate,
                UpdateBy = item.UpdateBy
            };
        }

        public async Task<ExamDetailResponseDto?> CreateAsync(ExamDetailCreateDto dto)
        {
            var entity = new ExamDetail
            {
                ExamId = dto.ExamId,
                Code = dto.Code,
                Status = dto.Status,
                CreateDate = dto.CreateDate,
                CreateBy = dto.CreateBy,
                UpdateDate = dto.UpdateDate,
                UpdateBy = dto.UpdateBy
            };

            var created = await _repository.CreateExamDetailAsync(entity);
            if (created == null)
                return null;

            return new ExamDetailResponseDto
            {
                Id = created.Id,
                ExamId = created.ExamId,
                Code = created.Code,
                Status = created.Status,
                CreateDate = created.CreateDate,
                CreateBy = created.CreateBy,
                UpdateDate = created.UpdateDate,
                UpdateBy = created.UpdateBy
            };
        }

        public async Task<ExamDetailResponseDto?> UpdateAsync(int id, ExamDetailCreateDto dto)
        {
            var entity = new ExamDetail
            {
                ExamId = dto.ExamId,
                Code = dto.Code,
                Status = dto.Status,
                CreateDate = dto.CreateDate,
                CreateBy = dto.CreateBy,
                UpdateDate = dto.UpdateDate,
                UpdateBy = dto.UpdateBy
            };

            var updated = await _repository.UpdateExamDetailAsync(id, entity);
            if (updated == null)
                return null;

            return new ExamDetailResponseDto
            {
                Id = updated.Id,
                ExamId = updated.ExamId,
                Code = updated.Code,
                Status = updated.Status,
                CreateDate = updated.CreateDate,
                CreateBy = updated.CreateBy,
                UpdateDate = updated.UpdateDate,
                UpdateBy = updated.UpdateBy
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var deleted = await _repository.DeleteExamDetailAsync(id);
            return deleted != null;
        }
    }
}
