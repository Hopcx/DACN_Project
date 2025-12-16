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
        Task<List<Level>> GetAllAsync();
        Task<Level?> GetByIdAsync(int id);
        Task AddAsync(Level level);
    }
}
