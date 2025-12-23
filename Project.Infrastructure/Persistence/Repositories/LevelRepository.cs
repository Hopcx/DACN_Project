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

        public async Task<List<Level>> GetAllLevelAsync()
        {
            return await _context.Levels.ToListAsync();
        }
        public async Task<Level> GetLevelByIdAsync(int id)
        {
            return await _context.Levels.FindAsync(id);
        }
        public async Task<Level> CreateLevelAsync(Level level)
        {
            try
            {
                var addLevel = _context.Levels.Add(level).Entity;
                await _context.SaveChangesAsync();
                return addLevel;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public async Task<Level> UpdateLevelAsync(Level level)
        {
            try
            {
                var objUpdateLevel = await _context.Levels.FindAsync(level.Id);

                objUpdateLevel.Name = level.Name;
                objUpdateLevel.Status = level.Status;


                var updateLevel = _context.Levels.Update(objUpdateLevel).Entity;
                await _context.SaveChangesAsync();
                return updateLevel;
            }
            catch
            {
                return null;
            }
        }
        public async Task<Level> DeleteLevelAsync(int id)
        {
            try
            {
                var objDeleteLevel = await _context.Levels.FindAsync(id);

                _context.Levels.Remove(objDeleteLevel);
                await _context.SaveChangesAsync();
                return objDeleteLevel;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<Level>> GetAllLevelId(int id)
        {
            return await _context.Levels.Where(x => x.Id == id).ToListAsync();
        }

        public async Task<List<User>> GetUserByIdLevel(int levelId, string? textSearch)
        {
            var query = _context.Users.AsQueryable();

            if (levelId >= 0)
            {
                query = query.Where(x => x.LevelId == levelId && x.Status == 1);
            }

            if (!string.IsNullOrWhiteSpace(textSearch))
            {
                query = query.Where(x => x.FullName.Contains(textSearch) || x.Email.Contains(textSearch) || x.PhoneNumber.Contains(textSearch) || x.UserName.Contains(textSearch) && x.Status == 1);
            }

            return await query.Where(x => x.Status == 1).ToListAsync();
        }
    }

}
