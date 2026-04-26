using Microsoft.EntityFrameworkCore;
using FileShareServer.Models;
using FileShareServer.Data;

namespace FileShareServer.Services
{
    public class FriendshipService
    {
        private readonly ApplicationDbContext _context;

        public FriendshipService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Friendship?> SendFriendRequestAsync(int userId, int friendId)
        {
            if (userId == friendId)
                return null;

            // Check if already friends/pending or reuse rejected relation.
            var existing = await _context.Friendships
                .FirstOrDefaultAsync(f => 
                    (f.UserId == userId && f.FriendId == friendId) ||
                    (f.UserId == friendId && f.FriendId == userId));

            if (existing != null)
            {
                if (existing.Status == FriendshipStatus.Accepted || existing.Status == FriendshipStatus.Pending)
                {
                    return null;
                }

                // Allow re-sending after rejection by resetting relation to new pending request.
                if (existing.Status == FriendshipStatus.Rejected)
                {
                    existing.UserId = userId;
                    existing.FriendId = friendId;
                    existing.Status = FriendshipStatus.Pending;
                    await _context.SaveChangesAsync();
                    return existing;
                }

                return null;
            }

            var friendship = new Friendship
            {
                UserId = userId,
                FriendId = friendId,
                Status = FriendshipStatus.Pending
            };

            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();
            return friendship;
        }

        public async Task<bool> AcceptFriendRequestAsync(int userId, int friendId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f => 
                    f.UserId == friendId && f.FriendId == userId && f.Status == FriendshipStatus.Pending);

            if (friendship == null)
                return false;

            friendship.Status = FriendshipStatus.Accepted;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectFriendRequestAsync(int userId, int friendId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f => 
                    f.UserId == friendId && f.FriendId == userId && f.Status == FriendshipStatus.Pending);

            if (friendship == null)
                return false;

            friendship.Status = FriendshipStatus.Rejected;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFriendAsync(int userId, int friendId)
        {
            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f => 
                    (f.UserId == userId && f.FriendId == friendId && f.Status == FriendshipStatus.Accepted) ||
                    (f.UserId == friendId && f.FriendId == userId && f.Status == FriendshipStatus.Accepted));

            if (friendship == null)
                return false;

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<User>> GetFriendsAsync(int userId)
        {
            var friendIds = await _context.Friendships
                .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == FriendshipStatus.Accepted)
                .Select(f => f.UserId == userId ? f.FriendId : f.UserId)
                .ToListAsync();

            return await _context.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();
        }

        public async Task<List<Friendship>> GetPendingRequestsAsync(int userId)
        {
            return await _context.Friendships
                .Where(f => f.FriendId == userId && f.Status == FriendshipStatus.Pending)
                .Include(f => f.User)
                .ToListAsync();
        }

        public async Task<List<Friendship>> GetSentRequestsAsync(int userId)
        {
            return await _context.Friendships
                .Where(f => f.UserId == userId && f.Status == FriendshipStatus.Pending)
                .Include(f => f.Friend)
                .ToListAsync();
        }

        public async Task<bool> AreFriendsAsync(int userId1, int userId2)
        {
            return await _context.Friendships
                .AnyAsync(f => 
                    ((f.UserId == userId1 && f.FriendId == userId2) ||
                    (f.UserId == userId2 && f.FriendId == userId1)) &&
                    f.Status == FriendshipStatus.Accepted);
        }
    }
}
