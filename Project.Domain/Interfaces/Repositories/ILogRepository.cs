using Project.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface ILogRepository
    {
        Task<List<Log>> GetAllLogsAsync();
        Task<Log> GetLogByIdAsync(int id);
        Task<Log> CreateLogAsync(Log log);
        Task<Log> UpdateLogAsync(int id, Log log);
        Task<Log> DeleteLogAsync(int id);
    }
}
