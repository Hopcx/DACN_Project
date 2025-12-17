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
    public class RoomRepository : IRoomRepository
    {
        private readonly ProjectDACNDbContext _context;
        public RoomRepository(ProjectDACNDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Room room)
        {
            try
            {
                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<List<Room>> GetAllAsync()
            => await _context.Rooms.AsNoTracking().ToListAsync();
        

        public async Task<Room?> GetByIdAsync(int id)
            => await _context.Rooms.FindAsync(id);
    }
}
