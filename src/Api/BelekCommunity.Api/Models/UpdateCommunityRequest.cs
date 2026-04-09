namespace BelekCommunity.Api.Models
{
    public class UpdateCommunityRequest
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string Status { get; set; } = "Active";
    }
}
