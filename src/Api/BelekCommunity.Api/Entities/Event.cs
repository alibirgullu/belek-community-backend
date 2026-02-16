using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Entities
{
    public class Event
    {
        public int Id { get; set; }

        public int CommunityId { get; set; }
        public Community Community { get; set; } = null!; // İlişki

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string? Location { get; set; }
        public string? PosterUrl { get; set; }

        public bool IsCancelled { get; set; } = false;
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}