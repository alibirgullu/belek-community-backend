using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    
    [Table("platform_users", Schema = "belek_student_community")]
    public class User
    {
        [Column("id")]
        public int Id { get; set; }

        
        [Column("external_user_id")]
        public int ExternalUserId { get; set; }

        [Column("student_number")]
        public string? StudentNumber { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("profile_image_url")]
        public string? ProfileImageUrl { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Active";

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}