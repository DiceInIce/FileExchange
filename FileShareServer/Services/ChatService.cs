using Microsoft.EntityFrameworkCore;
using FileShareServer.Models;
using FileShareServer.Data;

namespace FileShareServer.Services
{
    public class ChatService
    {
        private readonly ApplicationDbContext _context;

        public ChatService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ChatMessage> SendMessageAsync(int senderId, int receiverId, string content, MessageType type = MessageType.Text)
        {
            var message = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                Type = type,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<List<ChatMessage>> GetConversationAsync(int userId1, int userId2, int pageNumber = 1, int pageSize = 50)
        {
            return await _context.ChatMessages
                .Where(m => 
                    (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                    (m.SenderId == userId2 && m.ReceiverId == userId1))
                .OrderByDescending(m => m.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(int messageId)
        {
            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message == null)
                return false;

            message.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ChatMessage>> GetUnreadMessagesAsync(int userId)
        {
            return await _context.ChatMessages
                .Where(m => m.ReceiverId == userId && !m.IsRead)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<List<User>> GetConversationsAsync(int userId)
        {
            var userIds = await _context.ChatMessages
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .Select(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            return await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
        }
    }
}
