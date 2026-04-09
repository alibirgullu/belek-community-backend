using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BelekCommunity.Api.Services;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class NotificationsController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;
        private readonly IPushNotificationService _pushService;
        private readonly IConfiguration _configuration;

        public NotificationsController(BelekCommunityDbContext context, IPushNotificationService pushService, IConfiguration configuration)
        {
            _context = context;
            _pushService = pushService;
            _configuration = configuration;
        }

        // 1. Kullanıcının Tüm Bildirimlerini Getir
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            // Duyuru bitiş süresi: appsettings.json'dan oku (varsayılan 2 gün)
            int expiryDays = _configuration.GetValue<int>("AnnouncementSettings:ExpiryDays", 2);
            var expiryThreshold = DateTime.UtcNow.AddDays(-expiryDays);

            var notifications = await _context.Notifications
                .Where(n => n.PlatformUserId == currentUserId && !n.IsDeleted
                    // GlobalAnnouncement tipindeyse ve süresi dolmuşsa gösterme
                    && !(n.Type == "GlobalAnnouncement" && n.CreatedAt < expiryThreshold))
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // 1b. Duyuru Ayarlarını Getir (Admin)
        [HttpGet("settings")]
        public IActionResult GetAnnouncementSettings()
        {
            int expiryDays = _configuration.GetValue<int>("AnnouncementSettings:ExpiryDays", 2);
            return Ok(new { ExpiryDays = expiryDays });
        }

        // 1c. Duyuru Ayarlarını Güncelle (Admin)
        [HttpPut("settings")]
        public IActionResult UpdateAnnouncementSettings([FromBody] UpdateAnnouncementSettingsRequest request)
        {
            if (request.ExpiryDays < 1 || request.ExpiryDays > 30)
                return BadRequest(new { Message = "Geçerli değer aralığı: 1-30 gün." });

            // appsettings.json dosyasını güncelle
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "appsettings.json");
            
            // Daha güvenli: wwwroot yerine Content Root kullan
            var contentRoot = Directory.GetCurrentDirectory();
            var settingsFile = Path.Combine(contentRoot, "appsettings.json");

            if (!System.IO.File.Exists(settingsFile))
                return NotFound(new { Message = "Ayar dosyası bulunamadı." });

            var json = System.IO.File.ReadAllText(settingsFile);
            var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(json)!;
            jsonObj["AnnouncementSettings"]!["ExpiryDays"] = request.ExpiryDays;
            System.IO.File.WriteAllText(settingsFile, jsonObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            return Ok(new { Message = $"Duyuru görünürlük süresi {request.ExpiryDays} gün olarak güncellendi." });
        }

        // 2. Genel Duyuru Gönder (Super Admin)
        [HttpPost("global")]
        public async Task<IActionResult> CreateGlobal([FromBody] CreateGlobalNotificationRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            // Tüm aktif öğrencileri temel al
            var activeUsersQuery = _context.Users
                .Where(u => !u.IsDeleted && u.Status == "Active");

            if (request.TargetUserIds != null && request.TargetUserIds.Any())
            {
                // Yalnızca seçili öğrencilere gönder
                activeUsersQuery = activeUsersQuery.Where(u => request.TargetUserIds.Contains(u.Id));
            }
            else if (request.TargetCommunityId.HasValue && request.TargetCommunityId.Value > 0)
            {
                // Belirli bir topluluktaki öğrencilere gönder
                var communityMemberIds = await _context.CommunityMembers
                    .Where(cm => cm.CommunityId == request.TargetCommunityId.Value && !cm.IsDeleted)
                    .Select(cm => cm.PlatformUserId)
                    .ToListAsync();

                activeUsersQuery = activeUsersQuery.Where(u => communityMemberIds.Contains(u.Id));
            }

            var activeUsers = await activeUsersQuery.ToListAsync();

            if (!activeUsers.Any()) return BadRequest(new { Message = "Seçilen kriterlere uygun aktif kullanıcı bulunamadı." });

            var notifications = new List<Notification>();

            foreach (var user in activeUsers)
            {
                notifications.Add(new Notification
                {
                    PlatformUserId = user.Id,
                    Title = request.Title,
                    Message = request.Message,
                    Type = "GlobalAnnouncement", 
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.Notifications.AddRangeAsync(notifications);
            await _context.SaveChangesAsync();

            // Gerçek OS-Level Push Gönderimi
            var userIds = activeUsers.Select(u => u.Id).ToList();
            await _pushService.SendPushNotificationToUsersAsync(userIds, request.Title, request.Message);

            return Ok(new { Message = $"{activeUsers.Count} kişiye duyuru ve push bildirim gönderildi." });
        }

        public class CreateGlobalNotificationRequest
        {
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public int? TargetCommunityId { get; set; }
            public List<int>? TargetUserIds { get; set; }
        }

        // 3. Okunmamış Bildirim Sayısını Getir (Zil ikonundaki kırmızı sayı için)
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            // Sadece "Okunmamış" (IsRead == false) olanları sayıyoruz
            var count = await _context.Notifications
                .CountAsync(n => n.PlatformUserId == currentUserId && !n.IsRead && !n.IsDeleted);

            return Ok(new { UnreadCount = count });
        }

        // 3. Tek Bir Bildirimi "Okundu" Olarak İşaretle (Bildirime tıklayınca çalışır)
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.PlatformUserId == currentUserId);

            if (notification == null) return NotFound("Bildirim bulunamadı.");

            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Bildirim okundu olarak işaretlendi." });
        }

        // 4. Tümünü Okundu İşaretle (Temizle butonu)
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            // Adamın sadece okunmamış bildirimlerini bul
            var unreadNotifications = await _context.Notifications
                .Where(n => n.PlatformUserId == currentUserId && !n.IsRead && !n.IsDeleted)
                .ToListAsync();

            // Hepsini true yap
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Tüm bildirimler okundu olarak işaretlendi." });
        }

        // 5. Tüm Bildirimleri Temizle (Sil)
        [HttpDelete("clear-all")]
        public async Task<IActionResult> ClearAllNotifications()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var notifications = await _context.Notifications
                .Where(n => n.PlatformUserId == currentUserId && !n.IsDeleted)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsDeleted = true;
                notification.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Tüm bildirimler başarıyla temizlendi." });
        }

        // 6. Tek Bir Bildirimi Sil (Swipe to Delete)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.PlatformUserId == currentUserId);

            if (notification == null) return NotFound("Bildirim bulunamadı.");

            notification.IsDeleted = true;
            notification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Bildirim silindi." });
        }
    }
}

public class UpdateAnnouncementSettingsRequest
{
    public int ExpiryDays { get; set; }
}