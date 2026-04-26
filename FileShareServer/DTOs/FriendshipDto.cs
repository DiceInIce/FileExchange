namespace FileShareServer.DTOs
{
    public class FriendshipRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public UserDto? User { get; set; }
    }
}
