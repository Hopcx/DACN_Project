using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface IAnswerSubmissionRepository
    {
        Task<List<AnswerSubmission>> GetAllAnswerSubmissionsAsync();
        Task<AnswerSubmission> GetAnswerSubmissionByIdAsync(int id);
        Task<List<AnswerSubmission>> GetAnswerSubmissionsBySubmissionIdAsync(int submissionId);
        Task<AnswerSubmission> CreateAnswerSubmissionAsync(AnswerSubmission answerSubmission);
        Task<AnswerSubmission> UpdateAnswerSubmissionAsync(int id, AnswerSubmission answerSubmission);
        Task<AnswerSubmission> DeleteAnswerSubmissionAsync(int id);
    }
}
