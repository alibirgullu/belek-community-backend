using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Services
{
    public class CommunityMemberService : ICommunityMemberService
    {
        private readonly BelekCommunityDbContext _context;

        public CommunityMemberService(BelekCommunityDbContext context)
        {
            _context = context;
        }

        public async Task<(bool IsSuccess, string Message)> JoinCommunityAsync(int currentUserId, int communityId)
        {
            var community = await _context.Communities.FindAsync(communityId);
            if (community == null) return (false, "Topluluk bulunamadı.");

            var existingMembership = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId);

            if (existingMembership != null)
            {
                if (existingMembership.IsDeleted)
                {
                    // Eskiden silinmişse/reddedilmişse isteği tekrar aktifleştir
                    existingMembership.IsDeleted = false;
                    existingMembership.Status = "Pending";
                    await _context.SaveChangesAsync();
                    return (true, "Üyelik isteğiniz tekrar gönderildi.");
                }
                return (false, $"Zaten bir kaydınız var. Şu anki durumunuz: {existingMembership.Status}");
            }

            var defaultRole = await _context.CommunityRoles.FirstOrDefaultAsync(r => r.Name == "Member");
            if (defaultRole == null) return (false, "Sistemde 'Member' rolü tanımlı değil.");

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

            return (true, "Üyelik isteği gönderildi. Yöneticinin onayı bekleniyor.");
        }

        public async Task<object> GetMembersAsync(int communityId)
        {
            return await _context.CommunityMembers
                .Where(m => m.CommunityId == communityId && !m.IsDeleted && m.Status == "Active")
                .Include(m => m.PlatformUser)
                .Include(m => m.CommunityRole)
                .Select(m => new
                {
                    m.Id,
                    UserId = m.PlatformUserId,
                    FullName = m.PlatformUser.FirstName + " " + m.PlatformUser.LastName,
                    ProfileImageUrl = m.PlatformUser.ProfileImageUrl,
                    Role = m.CommunityRole.Name,
                    JoinedAt = m.CreatedAt
                })
                .ToListAsync();
        }

        // --- YENİ: BEKLEYENLERİ LİSTELE ---
        public async Task<(bool IsSuccess, string Message, object? Data)> GetPendingMembersAsync(int currentUserId, int communityId)
        {
            var adminMember = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            if (adminMember == null || !adminMember.CommunityRole.CanManageMembers)
                return (false, "Üye isteklerini görme yetkiniz bulunmamaktadır.", null);

            var pendingMembers = await _context.CommunityMembers
                .Where(m => m.CommunityId == communityId && !m.IsDeleted && m.Status == "Pending")
                .Include(m => m.PlatformUser)
                .Select(m => new
                {
                    UserId = m.PlatformUserId,
                    FullName = m.PlatformUser.FirstName + " " + m.PlatformUser.LastName,
                    ProfileImageUrl = m.PlatformUser.ProfileImageUrl,
                    RequestedAt = m.CreatedAt
                })
                .ToListAsync();

            return (true, "Başarılı", pendingMembers);
        }

        // --- YENİ: ONAYLA VEYA REDDET ---
        public async Task<(bool IsSuccess, string Message)> RespondToMembershipRequestAsync(int currentUserId, int communityId, int platformUserId, bool isApproved)
        {
            var adminMember = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            if (adminMember == null || !adminMember.CommunityRole.CanManageMembers)
                return (false, "Üye isteklerini yönetme yetkiniz bulunmamaktadır.");

            var targetMember = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == platformUserId && !m.IsDeleted && m.Status == "Pending");

            if (targetMember == null) return (false, "Bekleyen bir üyelik isteği bulunamadı.");

            var community = await _context.Communities.FindAsync(communityId);

            if (isApproved)
            {
                targetMember.Status = "Active";
                targetMember.UpdatedAt = DateTime.UtcNow;

                _context.Notifications.Add(new Notification
                {
                    PlatformUserId = platformUserId,
                    Title = "Üyelik Onaylandı!",
                    Message = $"{community?.Name} topluluğuna üyeliğiniz onaylandı.",
                    Type = "System",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                targetMember.IsDeleted = true; // Reddedileni soft delete ile gizliyoruz
                targetMember.Status = "Rejected";
                targetMember.UpdatedAt = DateTime.UtcNow;

                _context.Notifications.Add(new Notification
                {
                    PlatformUserId = platformUserId,
                    Title = "Üyelik Reddedildi",
                    Message = $"{community?.Name} topluluğuna üyelik isteğiniz yöneticiler tarafından reddedildi.",
                    Type = "System",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return (true, isApproved ? "Üye başarıyla onaylandı." : "Üyelik isteği reddedildi.");
        }

        public async Task<(bool IsSuccess, string Message)> RemoveMemberAsync(int currentUserId, int communityId, int platformUserId)
        {
            if (currentUserId != platformUserId)
            {
                var requestUserMembership = await _context.CommunityMembers
                    .Include(m => m.CommunityRole)
                    .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

                if (requestUserMembership == null || !requestUserMembership.CommunityRole.CanManageMembers)
                {
                    return (false, "Başka bir üyeyi gruptan çıkarma yetkiniz bulunmamaktadır.");
                }
            }

            var membership = await _context.CommunityMembers
                .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.PlatformUserId == platformUserId && !m.IsDeleted);

            if (membership == null) return (false, "Kayıt bulunamadı.");

            membership.IsDeleted = true;
            await _context.SaveChangesAsync();

            return (true, "İşlem başarılı. Kullanıcı topluluktan çıkarıldı.");
        }
    }
}