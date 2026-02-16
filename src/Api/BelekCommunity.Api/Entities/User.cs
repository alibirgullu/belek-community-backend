using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Entities
{
    public class User
    {
        public int Id { get; set; }

        // Şemada external_user_id var, sanırım ilerde auth servisi ayırırsan diye.
        // Şimdilik Guid tutabiliriz.
        public Guid ExternalUserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty; // Belek.edu.tr kontrolü business logic'te olacak

        public string? ProfileImageUrl { get; set; }

        public bool IsDeleted { get; set; } = false; // Soft Delete gereksinimi [cite: 316]

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // İlişkiler (Navigation Properties)
        public ICollection<CommunityMember> Memberships { get; set; } = new List<CommunityMember>();
    }
}