using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class CreateAnnouncementRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public string? TargetAudience { get; set; } = "Public";
    }
}