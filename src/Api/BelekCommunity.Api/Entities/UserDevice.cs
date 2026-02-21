using System.ComponentModel.DataAnnotations.Schema;

namespace BelekCommunity.Api.Entities
{
    [Table("user_devices")]
    public class UserDevice
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("platform_user_id")]
        public int PlatformUserId { get; set; }
        public User PlatformUser { get; set; } = null!;

        [Column("device_token")]
        public string DeviceToken { get; set; } = string.Empty; // Firebase/OneSignal Token'ı

        [Column("device_type")]
        public string? DeviceType { get; set; } // iOS, Android, Web

        [Column("device_name")]
        public string? DeviceName { get; set; }

        [Column("last_active_at")]
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}