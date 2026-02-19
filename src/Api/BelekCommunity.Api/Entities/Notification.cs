using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("notifications")]
    public class Notification
    {
        public int Id { get; set; }

        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User PlatformUser { get; set; } = null!; // İlişki

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        // Bildirimin türü (Örn: "Event", "System", "CommunityAnnouncement")
        public string? Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}