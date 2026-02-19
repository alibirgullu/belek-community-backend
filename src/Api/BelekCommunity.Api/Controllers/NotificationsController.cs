using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Sadece giriş yapmış kullanıcılar kendi bildirimlerini görebilir
    public class NotificationsController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public NotificationsController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // 1. Kullanıcının Tüm Bildirimlerini Getir
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var notifications = await _context.Notifications
                .Where(n => n.PlatformUserId == currentUserId && !n.IsDeleted)
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

        // 2. Okunmamış Bildirim Sayısını Getir (Zil ikonundaki kırmızı sayı için)
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
    }
}