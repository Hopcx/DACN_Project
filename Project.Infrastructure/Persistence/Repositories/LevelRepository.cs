using Microsoft.EntityFrameworkCore;
using Project.Domain.Entities;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.Repositories
{
    public class LevelRepository : ILevelRepository
    {
        private readonly ProjectDACNDbContext _context;

        public LevelRepository(ProjectDACNDbContext context)
        {
            _context = context;
        }

        public async Task<List<Level>> GetAllAsync()
            => await _context.Levels.AsNoTracking().ToListAsync();

        public async Task<Level?> GetByIdAsync(int id)
            => await _context.Levels.FindAsync(id);

        public async Task AddAsync(Level level)
        {
            _context.Levels.Add(level);
            await _context.SaveChangesAsync();
        }
    }

}
