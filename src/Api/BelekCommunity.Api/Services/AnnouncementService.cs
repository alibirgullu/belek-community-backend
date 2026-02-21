using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Services
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly BelekCommunityDbContext _context;

        public AnnouncementService(BelekCommunityDbContext context)
        {
            _context = context;
        }

        public async Task<object> GetAnnouncementsAsync(int communityId)
        {
            return await _context.Announcements
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
        }

        public async Task<(bool IsSuccess, string Message, int? AnnouncementId)> CreateAnnouncementAsync(int currentUserId, int communityId, CreateAnnouncementRequest request)
        {
            // 1. Topluluk kontrolü
            var community = await _context.Communities.FindAsync(communityId);
            if (community == null) return (false, "Topluluk bulunamadı.", null);

            // 2. Yetki Kontrolü
            var member = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            if (member == null || !member.CommunityRole.CanPostAnnouncement)
            {
                return (false, "Bu toplulukta duyuru paylaşma yetkiniz bulunmamaktadır.", null);
            }

            // 3. Duyuruyu Kaydet
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

            // 4. Bildirim Fırlatma (TRIGGER)
            List<int> targetUserIds = new List<int>();

            if (request.TargetAudience == "Public")
            {
                targetUserIds = await _context.Users
                    .Where(u => u.Status == "Active" && !u.IsDeleted && u.Id != currentUserId)
                    .Select(u => u.Id)
                    .ToListAsync();
            }
            else
            {
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

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }

            return (true, "Duyuru başarıyla yayınlandı ve bildirimler gönderildi.", announcement.Id);
        }
    }
}