namespace BelekCommunity.Api.Dtos
{
    public class CommunityMessageDto
    {
        public Guid Id { get; set; }
        public int CommunityId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderProfileImageUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsReadByCurrentUser { get; set; }
        public int ReadCount { get; set; }
    }
}
