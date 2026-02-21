namespace BelekCommunity.Api.Models
{
    public class CommunityDetailResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public int MemberCount { get; set; } // Toplam üye sayısı

        public List<CommunityMemberDto> BoardMembers { get; set; } = new();
        public List<CommunityEventDto> UpcomingEvents { get; set; } = new();
        public List<CommunityAnnouncementDto> RecentAnnouncements { get; set; } = new();
    }

    public class CommunityMemberDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class CommunityEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public string? Location { get; set; }
        public string? PosterUrl { get; set; }
    }

    public class CommunityAnnouncementDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}