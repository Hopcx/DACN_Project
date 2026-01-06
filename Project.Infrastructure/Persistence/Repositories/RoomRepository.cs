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
        public async Task<List<Room>> GetAllRoomAsync()
        {
            return await _context.Rooms.ToListAsync();
        }

        public async Task<Room> GetRoomByIdAsync(int id)
        {
            return await _context.Rooms.FindAsync(id);
        }

        public async Task<Room> CreateRoomAsync(Room r)
        {
            try
            {
                var addRoom = _context.Rooms.Add(r).Entity;
                await _context.SaveChangesAsync();
                return addRoom;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<Room> UpdateRoomAsync(Room r)
        {
            try
            {
                var updateRoom = _context.Rooms.Find(r.Id);

                updateRoom.Name = r.Name;
                updateRoom.Status = r.Status;
                updateRoom.Capacity = r.Capacity;
                var objRoom = _context.Rooms.Update(updateRoom).Entity;
                await _context.SaveChangesAsync();
                return objRoom;
            }
            catch (Exception)
            {

                return null;
            }
        }

        public async Task<Room> DeleteRoomAsync(int id)
        {
            try
            {
                var deleteRoom = _context.Rooms.Find(id);

                var objRoom = _context.Rooms.Remove(deleteRoom).Entity;
                await _context.SaveChangesAsync();
                return objRoom;
            }
            catch (Exception)
            {

                return null;
            }
        }
    }
}
