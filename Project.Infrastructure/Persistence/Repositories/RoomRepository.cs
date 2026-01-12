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

        public async Task<Room> UpdateRoomAsync(int id, Room r)
        {
            try
            {
                var roomUpdate = await _context.Rooms.FindAsync(id);
                if (roomUpdate == null)
                    return null;

                roomUpdate.Name = r.Name;
                roomUpdate.Status = r.Status;
                roomUpdate.Capacity = r.Capacity;
                roomUpdate.Address = r.Address;
                await _context.SaveChangesAsync();
                return roomUpdate;
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
                var deleteRoom = await _context.Rooms.FindAsync(id);

                _context.Rooms.Remove(deleteRoom);
                await _context.SaveChangesAsync();
                return deleteRoom;
            }
            catch (Exception)
            {

                return null;
            }
        }

        public async Task<(List<Room> Rooms, int TotalCount)> GetRoomsAsync(string? name, bool? status, int? minCapacity, int? maxCapacity, int page, int pageSize)
        {
            var query = _context.Rooms.AsQueryable();

            if (!string.IsNullOrEmpty(name))
                query = query.Where(r => r.Name.Contains(name));

            if (status.HasValue)
                query = query.Where(r => r.Status == status);

            if (minCapacity.HasValue)
                query = query.Where(r => r.Capacity >= minCapacity);

            if (maxCapacity.HasValue)
                query = query.Where(r => r.Capacity <= maxCapacity);

            var totalCount = await query.CountAsync();

            var rooms = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (rooms, totalCount);
        }
    }
}
