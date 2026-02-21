using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("ai_chat_logs")]
    public class AiChatLog
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User PlatformUser { get; set; } = null!;

        [Column("user_message")]
        public string UserMessage { get; set; } = string.Empty;

        [Column("bot_response")]
        public string BotResponse { get; set; } = string.Empty;

        [Column("intent")]
        public string? Intent { get; set; } // Örn: "etkinlik_sorma", "topluluk_arama"

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;
    }
}