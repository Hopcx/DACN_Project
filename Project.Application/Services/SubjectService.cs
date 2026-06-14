using Project.Application.DTOs.SubjectDTO;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class SubjectService : ISubjectService
    {
        private readonly ISubjectRepository _repository;

        public SubjectService(ISubjectRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SubjectResponseDto>> GetAllSubjectsAsync(string? textSearch, bool? isActive)
        {
            var subjects = await _repository.GetAllSubjectAsync(textSearch, isActive);
            return subjects.Select(x => new SubjectResponseDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Status = x.Status
            }).ToList();
        }

        public async Task<SubjectResponseDto?> GetSubjectByIdAsync(int id)
        {
            var subject = await _repository.GetSubjectByIdAsync(id);
            if (subject == null)
                return null;

            return new SubjectResponseDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description,
                Status = subject.Status
            };
        }

        public async Task<SubjectResponseDto?> CreateSubjectAsync(SubjectCreateDto dto)
        {
            var subject = new Subject
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var created = await _repository.CreateSubjectAsync(subject);
            if (created == null)
                return null;

            return new SubjectResponseDto
            {
                Id = created.Id,
                Name = created.Name,
                Description = created.Description,
                Status = created.Status
            };
        }

        public async Task<SubjectResponseDto?> UpdateSubjectAsync(int id, SubjectCreateDto dto)
        {
            var subject = new Subject
            {
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status
            };

            var updated = await _repository.UpdateSubjectAsync(id, subject);
            if (updated == null)
                return null;

            return new SubjectResponseDto
            {
                Id = updated.Id,
                Name = updated.Name,
                Description = updated.Description,
                Status = updated.Status
            };
        }

        public async Task<bool> DeleteSubjectAsync(int id)
        {
            var deleted = await _repository.DeleteSubjectAsync(id);
            return deleted != null;
        }
    }
}
