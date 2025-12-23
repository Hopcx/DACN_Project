using Project.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public interface ILevelRepository
    {
        Task<List<Level>> GetAllLevelAsync();
        Task<Level> GetLevelByIdAsync(int id);
        Task<Level> CreateLevelAsync(Level r);
        Task<Level> UpdateLevelAsync(Level r);
        Task<Level> DeleteLevelAsync(int id);
    }
}
