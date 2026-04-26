using Microsoft.EntityFrameworkCore;
using FileShareServer.Models;

namespace FileShareServer.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<FileTransfer> FileTransfers => Set<FileTransfer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.InitiatedFriendships)
                .WithOne(f => f.User)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.ReceivedFriendships)
                .WithOne(f => f.Friend)
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.Cascade);

            // ChatMessage relationships
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // FileTransfer relationships
            modelBuilder.Entity<FileTransfer>()
                .HasOne(f => f.Initiator)
                .WithMany()
                .HasForeignKey(f => f.InitiatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FileTransfer>()
                .HasOne(f => f.Recipient)
                .WithMany()
                .HasForeignKey(f => f.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
