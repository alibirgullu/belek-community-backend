using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class CreateEventRequest
    {
        [Required]
        public int CommunityId { get; set; } // Hangi topluluk düzenliyor?

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public string? Location { get; set; }

        public string? PosterUrl { get; set; }
    }
}