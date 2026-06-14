using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IAnswerRepository
    {
        Task<List<Answer>> GetAllAnswers();
        Task<List<Answer>> GetAllAnswerByQuestionId(int questionId);
        Task<Answer?> GetAnswerById(int id);
        Task<Answer> CreateAnswer(Answer answer);
        Task<Answer> UpdateAnswer(int id, Answer answer);
        Task<Answer> UpdateStatusAnswer(int answerId, byte status);

        Task<Answer> DeleteAnswer(int id);

    }
}
