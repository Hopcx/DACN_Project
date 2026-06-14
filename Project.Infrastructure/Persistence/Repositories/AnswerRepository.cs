using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using Project.Infrastructure.Persistence.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class AnswerRepository : IAnswerRepository
    {
        private readonly ProjectDACNDbContext _context;
        private readonly IADO _aDO;
        
        public AnswerRepository(ProjectDACNDbContext context, IADO aDO)
        {
            _context = context;
            _aDO = aDO;
        }
        public async Task<Answer> CreateAnswer(Answer answer)
        {
            try
            {
                answer.Content = answer.Content.Trim();
                var create = _context.Answers.Add(answer).Entity;
                await _context.SaveChangesAsync();
                return create;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Answer> DeleteAnswer(int id)
        {
            try
            {
                var objDeleteAnswer = await _context.Answers.FindAsync(id);
                var isInExamDetail = await _context.ExamDetailQuestions.AnyAsync(x => x.QuestionId == objDeleteAnswer.QuestionId);

                if (isInExamDetail)
                {
                    return null;
                }
                ;
                objDeleteAnswer.Status = 255;
                _context.Answers.Update(objDeleteAnswer);
                await _context.SaveChangesAsync();
                return objDeleteAnswer;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<Answer>> GetAllAnswerByQuestionId(int questionId)
        {
            return await _context.Answers.Where(x => x.QuestionId == questionId).ToListAsync();
        }

        public async Task<List<Answer>> GetAllAnswers()
        {
            return await _context.Answers.ToListAsync();
        }

        public async Task<Answer?> GetAnswerById(int id)
        {
            return await _context.Answers.FindAsync(id);
        }

        public async Task<Answer> UpdateAnswer(int id, Answer answer)
        {
            try
            {
                var obj = await _context.Answers.FindAsync(id);
                if (obj == null)
                    return null;
                obj.QuestionId = answer.QuestionId;
                obj.Content = answer.Content.Trim();
                obj.IsCorrect = answer.IsCorrect;
                obj.Status = answer.Status;
                obj.UpdatedBy = answer.UpdatedBy;
                obj.UpdatedAt = answer.UpdatedAt;

                var update = _context.Answers.Update(obj).Entity;
                await _context.SaveChangesAsync();
                return update;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Answer> UpdateStatusAnswer(int answerId, byte status)
        {
            try
            {
                var obj = await _context.Answers.FindAsync(answerId);

                obj.Status = status;

                var updateStatus = _context.Answers.Update(obj).Entity;
                await _context.SaveChangesAsync();
                return updateStatus;
            }
            catch
            {
                return null;
            }
        }
    }
}
