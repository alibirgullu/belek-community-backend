using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("user_auth", Schema = "public")]
    public class UserAuth
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("password_reset_token")]
        public string? PasswordResetToken { get; set; }

        [Column("password_reset_expires")]
        public DateTime? PasswordResetExpires { get; set; }

        [Column("temp_password")]
        public string? TempPassword { get; set; }

        [Column("must_change_password")]
        public bool MustChangePassword { get; set; }

        [Column("password_changed_by")]
        public int? PasswordChangedBy { get; set; }

        [Column("password_changed_at")]
        public DateTime? PasswordChangedAt { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("is_staff")]
        public bool IsStaff { get; set; }

        [Column("is_superuser")]
        public bool IsSuperuser { get; set; }

        public MainUser MainUser { get; set; } = null!;
    }
}
