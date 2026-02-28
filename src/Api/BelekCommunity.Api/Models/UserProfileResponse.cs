namespace BelekCommunity.Api.Models
{
    public class UserProfileResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string? Phone { get; set; }
        public string? Biography { get; set; } // platform_user_detail tablosundan gelecek
        public string? Department { get; set; }
        public List<UserCommunityDto> MyCommunities { get; set; } = new();
        public List<UserEventDto> UpcomingEvents { get; set; } = new();
    }

    public class UserCommunityDto
    {
        public int CommunityId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Active", "Pending" vb.
    }

    public class UserEventDto
    {
        public int EventId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CommunityName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public string? Location { get; set; }
        public string Status { get; set; } = string.Empty; // "Going", "Maybe" vb.
    }
}