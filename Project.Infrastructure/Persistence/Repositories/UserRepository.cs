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
    public class UserRepository : IUserRepository
    {
        private readonly ProjectDACNDbContext _context;
        public UserRepository(ProjectDACNDbContext context)
        {
            _context = context; 
        }
        public async Task<User> AddUserAsync(User user)
        {
            try
            {
                var addUser =  _context.Users.Add(user).Entity;
                await _context.SaveChangesAsync();
                return addUser;
            }
            catch (Exception)
            {

                return null;
            }
        }

        public async Task<bool> CheckEmailOrPhoneAsync(string email, string phoneNumber, string userName, Guid? userId)
        {
            if (userId != null)
            {
                return await _context.Users.AnyAsync(a => (a.Email == email || a.PhoneNumber == phoneNumber || a.UserName == userName) && a.Id != userId);
            }
            else
            {
                return await _context.Users.AnyAsync(a => a.Email == email || a.PhoneNumber == phoneNumber || a.UserName == userName);
            }
        }

        public async Task<User> DeleteUserAsync(Guid id)
        {
            try
            {
                var objDeleteUser = await _context.Users.FindAsync(id);
                _context.Users.Remove(objDeleteUser);
                await _context.SaveChangesAsync();
                return objDeleteUser;
            }
            catch
            {
                return null;
            }
        }

        public async Task<User> FindUserExistByKeyWordAsync(string key)
        {
            try
            {
                var u = _context.Users.FirstOrDefault(x => x.UserName.Equals(key) || x.Email.Equals(key) || x.PhoneNumber.Equals(key));

                if (u == null)
                    return null;
                return u;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.AsNoTracking().ToListAsync();
        }

        public async Task<User> GetByIdUserAsync(string id)
        {
            return await _context.Users.FindAsync(Guid.Parse(id));
        }

        public async Task<User> GetByIdUserSendMailAsync(Guid id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User> GetByKeyAndPasswordAsync(string keyword, string hashPassword)
        {
            User avaiableUser = await _context.Users.FirstOrDefaultAsync(x => x.UserName == keyword || x.Email == keyword || x.PhoneNumber == keyword);
            if (avaiableUser == null)
            {
                return null;
            }
            else
            {
                if (avaiableUser.PasswordHash.ToUpper() == hashPassword.ToUpper())
                {
                    return avaiableUser;
                }

                else
                {
                    ///wrong password
                    avaiableUser.PasswordHash = "-1";
                    return avaiableUser;
                }
            }
        }

        public async Task<List<User>> GetUsersByLevelIdAsync(int levelId)
        {
            return await _context.Users
                                 .Where(user => user.LevelId == levelId)
                                 .ToListAsync();
        }

        public async Task<List<User>> GetUsersNotInClassAsync(int classId, string? textSearch)
        {
            var usersNotInClass = await _context.Users
                .Where(u => u.Status == 1 && u.LevelId == 4 &&
                            !_context.ClassUsers.Any(cu => cu.UserId == u.Id && cu.ClassId == classId) &&
                            (string.IsNullOrEmpty(textSearch) || u.FullName.Contains(textSearch)))
                .ToListAsync();

            return usersNotInClass;
        }

        public async Task<List<User>> GetUsersWithStatusOneAsync(int classId, string? searchValue)
        {
            var usersWithStatusOne = await (from u in _context.Users
                                            join cu in _context.ClassUsers on u.Id equals cu.UserId
                                            where cu.Status == 1 && cu.ClassId == classId
                                            && (string.IsNullOrEmpty(searchValue) || u.FullName.Contains(searchValue))
                                            select u).ToListAsync();

            return usersWithStatusOne;
        }

        public async Task<List<User>> GetUsersWithStatusTwoAsync(int classId)
        {
            var usersWithStatusTwo = await (from u in _context.Users
                                            join cu in _context.ClassUsers on u.Id equals cu.UserId
                                            where cu.Status == 2 && cu.ClassId == classId
                                            select u).ToListAsync();

            return usersWithStatusTwo;
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            try
            {
                var objUpdateUser = await _context.Users.FindAsync(user.Id);

                objUpdateUser.FullName = user.FullName;
                objUpdateUser.UserName = user.UserName;
                objUpdateUser.DateOfBirth = user.DateOfBirth;
                objUpdateUser.PhoneNumber = user.PhoneNumber;
                objUpdateUser.Address = user.Address;
                objUpdateUser.Email = user.Email;
                objUpdateUser.PasswordHash = user.PasswordHash;
                objUpdateUser.AvatarUrl = user.AvatarUrl;

                var updateUser = _context.Users.Update(objUpdateUser).Entity;
                await _context.SaveChangesAsync();
                return updateUser;
            }
            catch
            {
                return null;
            }
        }
    }
}
