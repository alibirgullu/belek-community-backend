using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models; // CreateAnnouncementRequest gerekecek

namespace BelekCommunity.Api.Controllers
{
    [Route("api/communities/{communityId}/announcements")]
    [ApiController]
    public class AnnouncementsController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public AnnouncementsController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // 1. Duyuruları Listele (GET)
        [HttpGet]
        public async Task<IActionResult> GetAnnouncements(int communityId)
        {
            var announcements = await _context.Announcements
                .Where(a => a.CommunityId == communityId)
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

        // 2. Yeni Duyuru Oluştur (POST)
        [HttpPost]
        public async Task<IActionResult> Create(int communityId, [FromBody] CreateAnnouncementRequest request)
        {
            // --- GEÇİCİ YETKİ KONTROLÜ ---
            // Normalde burada "İstek yapan kişi bu topluluğun yöneticisi mi?" diye bakacağız.
            // Şimdilik herkes oluşturabilsin (Test için).
            // ------------------------------

            var community = await _context.Communities.FindAsync(communityId);
            if (community == null) return NotFound("Topluluk bulunamadı.");

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

            return Ok(new { Message = "Duyuru yayınlandı.", Id = announcement.Id });
        }
    }
}