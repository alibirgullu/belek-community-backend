using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("community_roles")]
    public class CommunityRole
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsExecutive { get; set; }
        public bool CanCreateEvent { get; set; }
        public bool CanManageMembers { get; set; }
        public bool CanPostAnnouncement { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}