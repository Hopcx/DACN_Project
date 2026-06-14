using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface ISubmissionRepository
    {
        Task<List<Submission>> GetAllSubmissionsAsync();
        Task<Submission> GetSubmissionByIdAsync(int id);
        Task<List<Submission>> GetSubmissionsByUserIdAsync(Guid userId);
        Task<Submission> CreateSubmissionAsync(Submission submission);
        Task<Submission> UpdateSubmissionAsync(int id, Submission submission);
        Task<Submission> DeleteSubmissionAsync(int id);
    }
}
