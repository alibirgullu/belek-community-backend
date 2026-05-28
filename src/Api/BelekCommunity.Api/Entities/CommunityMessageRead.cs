using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("community_message_reads")]
    public class CommunityMessageRead
    {
        [Column("message_id")]
        public Guid MessageId { get; set; }
        public CommunityMessage? Message { get; set; }

        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User? User { get; set; }

        [Column("read_at")]
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }
}
