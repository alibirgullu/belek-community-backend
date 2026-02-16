using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("announcements")]
    public class Announcement
    {
        public int Id { get; set; }

        public int CommunityId { get; set; }
        public Community Community { get; set; } = null!; // Navigation Property

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        // "MembersOnly", "Public" gibi değerler alabilir
        [Required]
        [Column("target_audience")]
        public string TargetAudience { get; set; } = "Public";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}