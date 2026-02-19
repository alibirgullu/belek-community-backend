using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("users", Schema = "public")]
    public class MainUser
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        public string? Username { get; set; } // Resimde var

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("user_type")]
        public string UserType { get; set; } = "Student";

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        // --- DOĞRULAMA İÇİN KULLANACAĞIMIZ MEVCUT KOLONLAR ---

        [Column("is_email_verified")]
        public bool IsEmailVerified { get; set; }

        [Column("email_verified_at")]
        public DateTime? EmailVerifiedAt { get; set; }

        // HİLE BURADA: Onay kodunu bu kolona yazacağız!
        [Column("password_reset_token")]
        public string? PasswordResetToken { get; set; }

        // Kodun süresi
        [Column("password_reset_expires")]
        public DateTime? PasswordResetExpires { get; set; }

        [Column("create_date")] // Resimde create_date yazıyor
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        [Column("update_date")]
        public DateTime? UpdateDate { get; set; }
    }
}