using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("event_participants")]
    public class EventParticipant
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("event_id")]
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User PlatformUser { get; set; } = null!;

        [Column("status")]
        public string Status { get; set; } = "Going"; // Going, Maybe, Cancelled vb.

        [Column("checked_in")]
        public bool CheckedIn { get; set; } = false; // Etkinliğe fiziksel olarak geldi mi? (Karekod için çok işine yarayacak)

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }
}