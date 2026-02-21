using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("user_refresh_tokens")]
    public class UserRefreshToken
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User PlatformUser { get; set; } = null!;

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [Column("replaced_by_token")]
        public string? ReplacedByToken { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}