using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("community_members")]
    public class CommunityMember
    {
        public int Id { get; set; }

        // Topluluk İlişkisi
        public int CommunityId { get; set; }
        public Community Community { get; set; } = null!;

        // Kullanıcı İlişkisi (Görselde platform_user_id olarak geçiyor)
        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User PlatformUser { get; set; } = null!;

        // YENİ: Rol İlişkisi (Senin tablodan gelen)
        [Column("community_role_id")]
        public int CommunityRoleId { get; set; }
        public CommunityRole CommunityRole { get; set; } = null!;

        [Required]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}