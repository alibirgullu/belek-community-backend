using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("community_members")] // Tablo adını garantiye alalım
    public class CommunityMember
    {
        public int Id { get; set; }

        public int CommunityId { get; set; }
        public Community Community { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Yönetici (Admin) mi yoksa Üye (Member) mi?
        [Required]
        public string Role { get; set; } = "Member";

        // YENİ: Başvuru durumu (Pending, Approved, Rejected)
        [Required]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}