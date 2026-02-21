using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    public class Community
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }
        public string? CoverImageUrl { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }
        public CommunityCategory? Category { get; set; }
        public string Status { get; set; } = "Pending";

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // İlişkiler
        public ICollection<CommunityMember> Members { get; set; } = new List<CommunityMember>();
        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}