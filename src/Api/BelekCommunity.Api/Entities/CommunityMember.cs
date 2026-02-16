namespace BelekCommunity.Api.Entities
{
    public class CommunityMember
    {
        public int Id { get; set; }
        public int CommunityId { get; set; }
        public Community Community { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        
        public string Role { get; set; } = "Member";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}