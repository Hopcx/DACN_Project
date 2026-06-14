using Project.Application.DTOs.AnswerCreateDto;
using Project.Application.Interfaces.Services;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.Services
{
    public class AnswerService : IAnswerService
    {
        private readonly IAnswerRepository _repository;
        public AnswerService(IAnswerRepository repository)
        {
            _repository = repository;
        }
        public async Task<AnswerResponseDto> CreateAnswerAsync(AnswerCreateDto dto)
        {
            var answer = new Answer
            {
                QuestionId = dto.QuestionId,
                Content = dto.Content,
                IsCorrect = dto.IsCorrect,
                Status = dto.Status,
                CreatedBy = dto.CreatedBy,
                UpdatedBy = dto.UpdatedBy,
                UpdatedAt = dto.UpdatedAt,
                CreatedAt = dto.CreatedAt
            };

            var createdAnswer = await _repository.CreateAnswer(answer);
            if(createdAnswer == null)
                return null;

            return new AnswerResponseDto
            {
                Id = createdAnswer.Id,
                QuestionId = createdAnswer.QuestionId,
                Content = createdAnswer.Content,
                IsCorrect = createdAnswer.IsCorrect,
                Status = createdAnswer.Status,
                CreatedBy = createdAnswer.CreatedBy,
                UpdatedBy = createdAnswer.UpdatedBy,
                UpdatedAt = createdAnswer.UpdatedAt,
                CreatedAt = createdAnswer.CreatedAt
            };
        }

        public async Task<bool> DeleteAnswerAsync(int id)
        {
            var deletedLevel = await _repository.DeleteAnswer(id);
            return deletedLevel != null;
        }

        public async Task<List<AnswerResponseDto>> GetAllAnswersAsync()
        {
            var answers = await _repository.GetAllAnswers();
            return answers.Select(a => new AnswerResponseDto
            {
                Id = a.Id,
                QuestionId = a.QuestionId,
                Content = a.Content,
                IsCorrect = a.IsCorrect,
                Status = a.Status,
                CreatedBy = a.CreatedBy,
                UpdatedBy = a.UpdatedBy,
                UpdatedAt = a.UpdatedAt,
                CreatedAt = a.CreatedAt
            }).ToList();
        }

        public async Task<AnswerResponseDto> UpdateAnswerAsync(int id, AnswerCreateDto dto)
        {
            var answer = new Answer
            {
                QuestionId = dto.QuestionId,
                Content = dto.Content,
                IsCorrect = dto.IsCorrect,
                Status = dto.Status,
                CreatedBy = dto.CreatedBy,
                UpdatedBy = dto.UpdatedBy,
                UpdatedAt = dto.UpdatedAt,
                CreatedAt = dto.CreatedAt
            };

            var updatedAnswer = await _repository.UpdateAnswer(id, answer);
            if (updatedAnswer == null)
                return null;

            return new AnswerResponseDto
            {
                Id = updatedAnswer.Id,
                QuestionId = updatedAnswer.QuestionId,
                Content = updatedAnswer.Content,
                IsCorrect = updatedAnswer.IsCorrect,
                Status = updatedAnswer.Status,
                CreatedBy = updatedAnswer.CreatedBy,
                UpdatedBy = updatedAnswer.UpdatedBy,
                UpdatedAt = updatedAnswer.UpdatedAt,
                CreatedAt = updatedAnswer.CreatedAt
            };
        }
    }
}
