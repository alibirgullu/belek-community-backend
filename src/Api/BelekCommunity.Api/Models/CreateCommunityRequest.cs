using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class CreateCommunityRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        public string? CoverImageUrl { get; set; }
    }
}