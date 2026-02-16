using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/communities/{communityId}/members")]
    [ApiController]
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
            // --- GEÇİCİ KOD (Auth gelene kadar) ---
            int currentUserId = 1;
            // --------------------------------

            var community = await _context.Communities.FindAsync(communityId);
            if (community == null) return NotFound("Topluluk bulunamadı.");

            // Zaten bir kaydı var mı?
            var existingMembership = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == currentUserId);

            if (existingMembership != null)
            {
                return BadRequest($"Zaten bir kaydınız var. Şu anki durumunuz: {existingMembership.Status}");
            }

            var membership = new CommunityMember
            {
                CommunityId = communityId,
                UserId = currentUserId,
                Role = "Member",       // Rol: Standart Üye
                Status = "Pending",    // Durum: Onay Bekliyor (Veritabanındaki kolona yazılacak)
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
                .Include(m => m.User)
                .Select(m => new
                {
                    m.Id,
                    UserId = m.UserId,
                    FullName = m.User.FirstName + " " + m.User.LastName,
                    ProfileImageUrl = m.User.ProfileImageUrl,
                    Role = m.Role,      // Örn: Member
                    Status = m.Status,  // Örn: Pending, Approved
                    JoinedAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(members);
        }

        // 3. Üyeyi Çıkar / İsteği İptal Et (DELETE)
        [HttpDelete("{userId}")]
        public async Task<IActionResult> RemoveMember(int communityId, int userId)
        {
            var membership = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == userId);

            if (membership == null) return NotFound("Kayıt bulunamadı.");

            // Soft Delete
            membership.IsDeleted = true;
            // İstersen statüyü de güncelleyebilirsin
            // membership.Status = "Removed"; 

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kullanıcı topluluktan çıkarıldı." });
        }
    }
}