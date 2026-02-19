using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims; // Token'dan ID okumak için

namespace BelekCommunity.Api.Controllers
{
    [Route("api/communities/{communityId}/members")]
    [ApiController]
    [Authorize] // KALKAN 1: Bu controller'a sadece giriş yapanlar ulaşabilir
    public class CommunityMembersController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public CommunityMembersController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // 1. Üyelik İsteği Gönder (POST)
        [HttpPost("join")]
        public async Task<IActionResult> JoinCommunity(int communityId)
        {
            // --- KALKAN 2: Sahte ID yerine Token'dan gerçek ID'yi okuyoruz ---
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);
            // ------------------------------------------------------------------

            var community = await _context.Communities.FindAsync(communityId);
            if (community == null) return NotFound("Topluluk bulunamadı.");

            var existingMembership = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId);

            if (existingMembership != null)
            {
                return BadRequest($"Zaten bir kaydınız var. Şu anki durumunuz: {existingMembership.Status}");
            }

            var defaultRole = await _context.CommunityRoles.FirstOrDefaultAsync(r => r.Name == "Member");
            if (defaultRole == null)
            {
                return BadRequest("Sistemde 'Member' rolü tanımlı değil. Lütfen veritabanına ekleyin.");
            }

            var membership = new CommunityMember
            {
                CommunityId = communityId,
                PlatformUserId = currentUserId,
                CommunityRoleId = defaultRole.Id,
                Status = "Pending",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.CommunityMembers.Add(membership);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Üyelik isteği gönderildi. Yöneticinin onayı bekleniyor." });
        }

        // 2. Üyeleri Listele (GET)
        [HttpGet]
        public async Task<IActionResult> GetMembers(int communityId)
        {
            var members = await _context.CommunityMembers
                .Where(m => m.CommunityId == communityId && !m.IsDeleted)
                .Include(m => m.PlatformUser)
                .Include(m => m.CommunityRole)
                .Select(m => new
                {
                    m.Id,
                    UserId = m.PlatformUserId,
                    FullName = m.PlatformUser.FirstName + " " + m.PlatformUser.LastName,
                    ProfileImageUrl = m.PlatformUser.ProfileImageUrl,
                    Role = m.CommunityRole.Name,
                    Status = m.Status,
                    JoinedAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(members);
        }

        // 3. Üyeyi Çıkar / İsteği İptal Et (DELETE)
        [HttpDelete("{platformUserId}")]
        public async Task<IActionResult> RemoveMember(int communityId, int platformUserId)
        {
            // 1. İsteği Yapanın Kimliğini Al
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int requestUserId = int.Parse(userIdString);

            // --- KALKAN 3: YETKİ KONTROLÜ ---
            // Eğer isteği yapan kişi (requestUserId) ile silinmek istenen kişi (platformUserId) FARKLIYSA,
            // demek ki birisi başkasını gruptan atmaya çalışıyor. O zaman "Yönetici" yetkisine bakalım:
            if (requestUserId != platformUserId)
            {
                var requestUserMembership = await _context.CommunityMembers
                    .Include(m => m.CommunityRole)
                    .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == requestUserId && !m.IsDeleted);

                // Bu adam üye değilse veya CanManageMembers (Üye Yönetimi) yetkisi yoksa yasakla!
                if (requestUserMembership == null || !requestUserMembership.CommunityRole.CanManageMembers)
                {
                    return StatusCode(403, new { Message = "Başka bir üyeyi gruptan çıkarma yetkiniz bulunmamaktadır." });
                }
            }
            // (Eğer kendi kendini siliyorsa yukarıdaki if bloğuna girmez, direkt silinir - gruptan çıkma mantığı)

            // 2. Silinecek kaydı bul
            var membership = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == platformUserId);

            if (membership == null) return NotFound("Kayıt bulunamadı.");

            // 3. Soft Delete yap
            membership.IsDeleted = true;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "İşlem başarılı. Kullanıcı topluluktan çıkarıldı." });
        }
    }
}