using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/communities/{communityId}/announcements")]
    [ApiController]
    [Authorize] // Bu controller içindeki her şey için giriş yapmak zorunlu
    public class AnnouncementsController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public AnnouncementsController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // 1. Duyuruları Listele
        [HttpGet]
        public async Task<IActionResult> GetAnnouncements(int communityId)
        {
            var announcements = await _context.Announcements
                .Where(a => a.CommunityId == communityId && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Content,
                    a.TargetAudience,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(announcements);
        }

        // 2. Yeni Duyuru Oluştur (YETKİ KALKANI VE BİLDİRİM TETİKLEYİCİSİ EKLENDİ)
        [HttpPost]
        public async Task<IActionResult> Create(int communityId, [FromBody] CreateAnnouncementRequest request)
        {
            // 1. Adım: Token'dan ID al
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            // 2. Adım: Topluluk kontrolü
            var community = await _context.Communities.FindAsync(communityId);
            if (community == null) return NotFound("Topluluk bulunamadı.");

            // 3. Adım: YETKİ KONTROLÜ
            var member = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            // Üye değilse veya 'CanPostAnnouncement' yetkisi yoksa reddet
            if (member == null || !member.CommunityRole.CanPostAnnouncement)
            {
                return StatusCode(403, new { Message = "Bu toplulukta duyuru paylaşma yetkiniz bulunmamaktadır." });
            }

            // 4. Adım: Duyuruyu Kaydet
            var announcement = new Announcement
            {
                CommunityId = communityId,
                Title = request.Title,
                Content = request.Content,
                TargetAudience = request.TargetAudience ?? "Public",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            // --- 5. BİLDİRİM FIRLATMA (TRIGGER) ---
            List<int> targetUserIds = new List<int>();

            if (request.TargetAudience == "Public")
            {
                // Eğer "Public" ise (SKS veya Genel Duyuru), okuldaki tüm aktif öğrencilere gönder
                targetUserIds = await _context.Users
                    .Where(u => u.Status == "Active" && !u.IsDeleted && u.Id != currentUserId)
                    .Select(u => u.Id)
                    .ToListAsync();
            }
            else
            {
                // Eğer "MembersOnly" ise, sadece bu topluluğun üyelerine gönder
                targetUserIds = await _context.CommunityMembers
                    .Where(m => m.CommunityId == communityId && !m.IsDeleted && m.PlatformUserId != currentUserId)
                    .Select(m => m.PlatformUserId)
                    .ToListAsync();
            }

            if (targetUserIds.Any())
            {
                var notifications = targetUserIds.Select(userId => new Notification
                {
                    PlatformUserId = userId,
                    Title = request.TargetAudience == "Public" ? "Genel Duyuru" : "Topluluk Duyurusu",
                    Message = $"{community.Name} yeni bir duyuru yayınladı: {request.Title}",
                    Type = "Announcement",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }).ToList();

                // Yüzlerce veya binlerce bildirimi tek sorguda veritabanına yazıyoruz (Bulk Insert)
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }
            // ---------------------------------------

            return Ok(new { Message = "Duyuru başarıyla yayınlandı ve bildirimler gönderildi.", Id = announcement.Id });
        }
    }
}