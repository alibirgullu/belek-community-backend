namespace BelekCommunity.Api.Models
{
    public class UpdateProfileRequest
    {
        public string? ProfileImageUrl { get; set; }
        public string? Phone { get; set; }
        public string? Biography { get; set; }
    }
}