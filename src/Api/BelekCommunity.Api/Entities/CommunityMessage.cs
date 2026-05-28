using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("community_messages")]
    public class CommunityMessage
    {
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("community_id")]
        public int CommunityId { get; set; }
        public Community? Community { get; set; }

        [Column("sender_id")]
        public int SenderId { get; set; }
        public User? Sender { get; set; }

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        public ICollection<CommunityMessageRead> Reads { get; set; } = new List<CommunityMessageRead>();
    }
}
