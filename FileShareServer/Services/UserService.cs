using Microsoft.EntityFrameworkCore;
using FileShareServer.Models;
using FileShareServer.Data;

namespace FileShareServer.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SetUserOnlineAsync(int userId, string connectionId)
        {
            var user = await GetUserByIdAsync(userId);
            if (user != null)
            {
                user.IsOnline = true;
                user.ConnectionId = connectionId;
                await UpdateUserAsync(user);
            }
        }

        public async Task SetUserOfflineAsync(int userId)
        {
            var user = await GetUserByIdAsync(userId);
            if (user != null)
            {
                user.IsOnline = false;
                user.ConnectionId = null;
                await UpdateUserAsync(user);
            }
        }

        public async Task<List<User>> SearchUsersAsync(string query)
        {
            return await _context.Users
                .Where(u => u.Username.Contains(query) || u.DisplayName!.Contains(query))
                .ToListAsync();
        }
    }
}
