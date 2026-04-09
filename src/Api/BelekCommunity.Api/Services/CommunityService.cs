using BelekCommunity.Api.Data;
using BelekCommunity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly BelekCommunityDbContext _context;

        public CommunityService(BelekCommunityDbContext context)
        {
            _context = context;
        }

        public async Task<CommunityDetailResponse?> GetCommunityDetailsAsync(int communityId)
        {
            // 1. Topluluğun Ana Bilgilerini Çek
            var community = await _context.Communities
                .Where(c => c.Id == communityId && !c.IsDeleted)
                .FirstOrDefaultAsync();

            if (community == null) return null;

            // 2. SADECE Yönetim Kurulu Üyelerini Çek (IsExecutive == true)
            var boardMembers = await _context.CommunityMembers
                .Include(m => m.PlatformUser)
                .Include(m => m.CommunityRole)
                .Where(m => m.CommunityId == communityId && !m.IsDeleted && m.Status == "Active" && m.CommunityRole.IsExecutive)
                .Select(m => new CommunityMemberDto
                {
                    UserId = m.PlatformUserId,
                    FullName = $"{m.PlatformUser.FirstName} {m.PlatformUser.LastName}",
                    ProfileImageUrl = m.PlatformUser.ProfileImageUrl,
                    RoleName = m.CommunityRole.Name
                })
                .ToListAsync();

            // 3. Yaklaşan En Yakın 5 Etkinliği Çek 
            var upcomingEvents = await _context.Events
                .Where(e => e.CommunityId == communityId && !e.IsDeleted && !e.IsCancelled && e.StartDate >= DateTime.UtcNow)
                .OrderBy(e => e.StartDate)
                .Take(5)
                .Select(e => new CommunityEventDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    StartDate = e.StartDate,
                    Location = e.Location,
                    PosterUrl = e.PosterUrl
                })
                .ToListAsync();

            // 4. Son 5 Duyuruyu Çek
            var recentAnnouncements = await _context.Announcements
                .Where(a => a.CommunityId == communityId && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new CommunityAnnouncementDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Content = a.Content,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            // 5. Toplam Onaylı Üye Sayısı
            var memberCount = await _context.CommunityMembers
                .CountAsync(m => m.CommunityId == communityId && !m.IsDeleted && m.Status == "Active");

            // Hepsini DTO'da birleştir ve gönder!
            return new CommunityDetailResponse
            {
                Id = community.Id,
                Name = community.Name,
                Description = community.Description,
                LogoUrl = community.LogoUrl,
                CoverImageUrl = community.CoverImageUrl,
                MemberCount = memberCount,
                BoardMembers = boardMembers,
                UpcomingEvents = upcomingEvents,
                RecentAnnouncements = recentAnnouncements
            };
        }

        public async Task<(bool IsSuccess, string Message)> UpdateCommunityAsync(int currentUserId, IList<string> currentRoles, int communityId, UpdateCommunityRequest request)
        {
            // 1. Yetki Kontrolü: Bu kişi bu topluluğun lideri mi? (CanEditCommunity vb.)
            var member = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            
            bool isSuperAdmin = currentRoles.Contains("SuperAdmin");

            
            if (!isSuperAdmin && (member == null || member.CommunityRole.Name != "Admin"))
            {
                return (false, "Bu topluluğun bilgilerini güncelleme yetkiniz bulunmamaktadır.");
            }

            try
            {
                
                await _context.Database.ExecuteSqlRawAsync(
                    "CALL belek_student_community.sp_update_community({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                    communityId,
                    request.CategoryId,
                    request.Name,
                    request.Description,
                    request.LogoUrl ?? "",
                    request.CoverImageUrl ?? "",
                    request.Status
                );

                return (true, "Topluluk bilgileri başarıyla güncellendi.");
            }
            catch (Exception ex)
            {
                
                return (false, $"Güncelleme sırasında bir hata oluştu: {ex.Message}");
            }
        }
    }
}