using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public EventsController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // 1. Tüm Etkinlikleri Listele (Giriş yapmayanlar bile görebilir)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var events = await _context.Events
                                       .Include(e => e.Community)
                                       .Where(e => !e.IsDeleted && !e.IsCancelled)
                                       .OrderByDescending(e => e.StartDate)
                                       .ToListAsync();
            return Ok(events);
        }

        // 2. Yeni Etkinlik Oluştur (YETKİ KALKANI VE BİLDİRİM TETİKLEYİCİSİ EKLENDİ)
        [HttpPost]
        [Authorize] // Sadece giriş yapanlar buraya girebilir
        public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. Adım: Token'dan kullanıcının ID'sini (PlatformUserId) alıyoruz
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Kullanıcı kimliği doğrulanamadı.");
            int currentUserId = int.Parse(userIdString);

            // 2. Adım: Topluluk var mı kontrolü
            var community = await _context.Communities.FindAsync(request.CommunityId);
            if (community == null)
                return NotFound("Belirtilen ID'ye sahip topluluk bulunamadı.");

            // 3. Adım: YETKİ KONTROLÜ
            var member = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == request.CommunityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            // Eğer üye değilse veya 'CanCreateEvent' yetkisi yoksa kapı dışarı!
            if (member == null || !member.CommunityRole.CanCreateEvent)
            {
                return StatusCode(403, new { Message = "Bu toplulukta etkinlik oluşturma yetkiniz bulunmamaktadır." });
            }

            // 4. Adım: Yetkisi varsa etkinliği kaydet
            var newEvent = new Event
            {
                CommunityId = request.CommunityId,
                Title = request.Title,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Location = request.Location,
                PosterUrl = request.PosterUrl,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            // --- 5. BİLDİRİM FIRLATMA (TRIGGER) ---
            // Topluluktaki tüm aktif üyeleri buluyoruz (Etkinliği oluşturan kişi hariç tutuluyor)
            var memberIds = await _context.CommunityMembers
                .Where(m => m.CommunityId == request.CommunityId && !m.IsDeleted && m.PlatformUserId != currentUserId)
                .Select(m => m.PlatformUserId)
                .ToListAsync();

            if (memberIds.Any())
            {
                var notifications = new List<Notification>();
                foreach (var memberId in memberIds)
                {
                    notifications.Add(new Notification
                    {
                        PlatformUserId = memberId,
                        Title = "Yeni Etkinlik!",
                        Message = $"{community.Name} yeni bir etkinlik oluşturdu: {request.Title}",
                        Type = "Event",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    });
                }

                // AddRange ile yüzlerce bildirimi tek bir SQL sorgusuyla hızlıca kaydediyoruz
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }
            // ---------------------------------------

            return Ok(new { message = "Etkinlik başarıyla oluşturuldu ve üyelere bildirim gönderildi.", eventId = newEvent.Id });
        }
    }
}