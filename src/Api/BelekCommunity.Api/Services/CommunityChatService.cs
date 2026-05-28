using BelekCommunity.Api.Data;
using BelekCommunity.Api.Dtos;
using BelekCommunity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Services
{
    public class CommunityChatService : ICommunityChatService
    {
        private readonly BelekCommunityDbContext _context;
        private readonly IPushNotificationService _pushService;

        public CommunityChatService(BelekCommunityDbContext context, IPushNotificationService pushService)
        {
            _context = context;
            _pushService = pushService;
        }

        public async Task<(bool IsSuccess, string Message, CommunityMessageDto? Data)> SaveMessageAsync(int communityId, int senderId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return (false, "Mesaj içeriği boş olamaz.", null);

            var isMember = await _context.CommunityMembers
                .AnyAsync(m => m.CommunityId == communityId && m.PlatformUserId == senderId && !m.IsDeleted && m.Status == "Active");

            if (!isMember)
                return (false, "Bu topluluğa mesaj gönderme yetkiniz yok.", null);

            var now = DateTime.UtcNow;
            var message = new CommunityMessage
            {
                CommunityId = communityId,
                SenderId = senderId,
                Content = content,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.CommunityMessages.Add(message);
            await _context.SaveChangesAsync();

            var sender = await _context.Users.FindAsync(senderId);

            var dto = new CommunityMessageDto
            {
                Id = message.Id,
                CommunityId = message.CommunityId,
                SenderId = message.SenderId,
                SenderName = sender != null ? $"{sender.FirstName} {sender.LastName}" : "Bilinmeyen Kullanıcı",
                SenderProfileImageUrl = sender?.ProfileImageUrl,
                Content = message.Content,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                IsEdited = false,
                IsReadByCurrentUser = true,
                ReadCount = 0
            };

            var memberIds = await _context.CommunityMembers
                .Where(m => m.CommunityId == communityId && m.PlatformUserId != senderId && !m.IsDeleted && m.Status == "Active")
                .Select(m => m.PlatformUserId)
                .ToListAsync();

            if (memberIds.Any())
            {
                var communityName = await _context.Communities.Where(c => c.Id == communityId).Select(c => c.Name).FirstOrDefaultAsync();
                string title = $"{communityName} - Yeni Mesaj";
                string body = $"{dto.SenderName}: {content}";

                _ = _pushService.SendPushNotificationToUsersAsync(memberIds, title, body, new { type = "chat_message", communityId = communityId });
            }

            return (true, "Mesaj başarıyla kaydedildi.", dto);
        }

        public async Task<(bool IsSuccess, string Message, object? Data)> GetCommunityMessagesAsync(int communityId, int currentUserId, int page = 1, int pageSize = 50)
        {
            var isMember = await _context.CommunityMembers
                .AnyAsync(m => m.CommunityId == communityId && m.PlatformUserId == currentUserId && !m.IsDeleted && m.Status == "Active");

            if (!isMember)
                return (false, "Bu topluluğun mesajlarını görme yetkiniz yok.", null);

            var query = _context.CommunityMessages
                .Where(m => m.CommunityId == communityId)
                .OrderByDescending(m => m.CreatedAt);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var messages = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new CommunityMessageDto
                {
                    Id = m.Id,
                    CommunityId = m.CommunityId,
                    SenderId = m.SenderId,
                    SenderName = m.Sender != null ? $"{m.Sender.FirstName} {m.Sender.LastName}" : "Bilinmeyen Kullanıcı",
                    SenderProfileImageUrl = m.Sender != null ? m.Sender.ProfileImageUrl : null,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt,
                    IsEdited = m.UpdatedAt.HasValue && m.UpdatedAt.Value > m.CreatedAt,
                    IsReadByCurrentUser = m.SenderId == currentUserId
                                           || m.Reads.Any(r => r.PlatformUserId == currentUserId && !r.IsDeleted),
                    ReadCount = m.Reads.Count(r => !r.IsDeleted)
                })
                .ToListAsync();

            messages.Reverse();

            return (true, "Mesajlar getirildi.", new
            {
                Messages = messages,
                Pagination = new
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages
                }
            });
        }

        public async Task<(bool IsSuccess, string Message, List<Guid> MarkedIds)> MarkMessagesAsReadAsync(int communityId, int userId, List<Guid> messageIds)
        {
            if (messageIds == null || messageIds.Count == 0)
                return (true, "İşlenecek mesaj yok.", new List<Guid>());

            var isMember = await _context.CommunityMembers
                .AnyAsync(m => m.CommunityId == communityId && m.PlatformUserId == userId && !m.IsDeleted && m.Status == "Active");

            if (!isMember)
                return (false, "Bu topluluğa erişim yetkiniz yok.", new List<Guid>());

            var validMessageIds = await _context.CommunityMessages
                .Where(m => messageIds.Contains(m.Id) && m.CommunityId == communityId && m.SenderId != userId)
                .Select(m => m.Id)
                .ToListAsync();

            if (validMessageIds.Count == 0)
                return (true, "İşlenecek geçerli mesaj yok.", new List<Guid>());

            var alreadyReadIds = await _context.CommunityMessageReads
                .Where(r => validMessageIds.Contains(r.MessageId) && r.PlatformUserId == userId && !r.IsDeleted)
                .Select(r => r.MessageId)
                .ToListAsync();

            var toAdd = validMessageIds.Except(alreadyReadIds).ToList();

            if (toAdd.Count > 0)
            {
                var now = DateTime.UtcNow;
                var reads = toAdd.Select(id => new CommunityMessageRead
                {
                    MessageId = id,
                    PlatformUserId = userId,
                    ReadAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                await _context.CommunityMessageReads.AddRangeAsync(reads);
                await _context.SaveChangesAsync();
            }

            return (true, "Okundu olarak işaretlendi.", toAdd);
        }

        public async Task<int> GetUnreadCountAsync(int communityId, int userId)
        {
            var isMember = await _context.CommunityMembers
                .AnyAsync(m => m.CommunityId == communityId && m.PlatformUserId == userId && !m.IsDeleted && m.Status == "Active");

            if (!isMember) return 0;

            return await _context.CommunityMessages
                .Where(m => m.CommunityId == communityId
                         && m.SenderId != userId
                         && !m.Reads.Any(r => r.PlatformUserId == userId && !r.IsDeleted))
                .CountAsync();
        }

        public async Task<(bool IsSuccess, string Message, CommunityMessageDto? Data)> EditMessageAsync(Guid messageId, int userId, string newContent)
        {
            if (string.IsNullOrWhiteSpace(newContent))
                return (false, "Mesaj içeriği boş olamaz.", null);

            var message = await _context.CommunityMessages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
                return (false, "Mesaj bulunamadı.", null);

            if (message.SenderId != userId)
                return (false, "Bu mesajı düzenleme yetkiniz yok.", null);

            message.Content = newContent;
            message.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var readCount = await _context.CommunityMessageReads
                .CountAsync(r => r.MessageId == message.Id && !r.IsDeleted);

            var dto = new CommunityMessageDto
            {
                Id = message.Id,
                CommunityId = message.CommunityId,
                SenderId = message.SenderId,
                SenderName = message.Sender != null ? $"{message.Sender.FirstName} {message.Sender.LastName}" : "Bilinmeyen Kullanıcı",
                SenderProfileImageUrl = message.Sender?.ProfileImageUrl,
                Content = message.Content,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                IsEdited = true,
                IsReadByCurrentUser = true,
                ReadCount = readCount
            };

            return (true, "Mesaj güncellendi.", dto);
        }

        public async Task<(bool IsSuccess, string Message, int CommunityId)> DeleteMessageAsync(Guid messageId, int userId)
        {
            var message = await _context.CommunityMessages
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
                return (false, "Mesaj bulunamadı.", 0);

            var canDelete = message.SenderId == userId;
            if (!canDelete)
            {
                canDelete = await _context.CommunityMembers
                    .Include(m => m.CommunityRole)
                    .AnyAsync(m => m.CommunityId == message.CommunityId
                                && m.PlatformUserId == userId
                                && !m.IsDeleted
                                && m.Status == "Active"
                                && (m.CommunityRole.IsExecutive
                                    || m.CommunityRole.Name == "Admin"
                                    || m.CommunityRole.Name == "Başkan"));
            }

            if (!canDelete)
                return (false, "Bu mesajı silme yetkiniz yok.", 0);

            message.IsDeleted = true;
            message.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (true, "Mesaj silindi.", message.CommunityId);
        }
    }
}
