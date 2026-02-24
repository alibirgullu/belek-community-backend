using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Services
{
    public class EventService : IEventService
    {
        private readonly BelekCommunityDbContext _context;
        private readonly EmailService _emailService;

        public EventService(BelekCommunityDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<IEnumerable<Event>> GetAllEventsAsync()
        {
            return await _context.Events
                                 .Include(e => e.Community)
                                 .Where(e => !e.IsDeleted && !e.IsCancelled)
                                 .OrderByDescending(e => e.StartDate)
                                 .ToListAsync();
        }

        public async Task<(bool IsSuccess, string Message, int? EventId)> CreateEventAsync(int currentUserId, CreateEventRequest request)
        {
            // 1. Topluluk var mı?
            var community = await _context.Communities.FindAsync(request.CommunityId);
            if (community == null)
                return (false, "Belirtilen ID'ye sahip topluluk bulunamadı.", null);

            // 2. Yetki Kontrolü
            var member = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == request.CommunityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            if (member == null || !member.CommunityRole.CanCreateEvent)
            {
                return (false, "Bu toplulukta etkinlik oluşturma yetkiniz bulunmamaktadır.", null);
            }

            // 3. Etkinliği Kaydet
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

            // 4. Bildirim Fırlatma
            var memberIds = await _context.CommunityMembers
                .Where(m => m.CommunityId == request.CommunityId && !m.IsDeleted && m.PlatformUserId != currentUserId)
                .Select(m => m.PlatformUserId)
                .ToListAsync();

            if (memberIds.Any())
            {
                var notifications = memberIds.Select(memberId => new Notification
                {
                    PlatformUserId = memberId,
                    Title = "Yeni Etkinlik!",
                    Message = $"{community.Name} yeni bir etkinlik oluşturdu: {request.Title}",
                    Type = "Event",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                }).ToList();

                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }

            return (true, "Etkinlik başarıyla oluşturuldu ve üyelere bildirim gönderildi.", newEvent.Id);
        }

        public async Task<(bool IsSuccess, string Message)> ToggleEventParticipationAsync(int currentUserId, int eventId)
        {
            // 1. Etkinlik gerçekten var mı ve aktif mi?
            var targetEvent = await _context.Events.FindAsync(eventId);
            if (targetEvent == null || targetEvent.IsDeleted)
                return (false, "Etkinlik bulunamadı.");

            if (targetEvent.IsCancelled)
                return (false, "Bu etkinlik iptal edilmiş, katılım sağlanamaz.");

            // 2. Kullanıcı daha önce bu etkinliğe katılmış mı?
            var existingParticipation = await _context.EventParticipants
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.PlatformUserId == currentUserId && !ep.IsDeleted);

            if (existingParticipation != null)
            {
                // Zaten katılmış! Demek ki vazgeçmek (katılımı iptal etmek) istiyor.
                existingParticipation.IsDeleted = true;
                existingParticipation.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return (true, "Etkinlik katılımınız iptal edildi.");
            }

            // 3. Daha önce katılmamış, yeni kayıt oluşturuyoruz.
            var participant = new EventParticipant
            {
                EventId = eventId,
                PlatformUserId = currentUserId,
                Status = "Going",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.EventParticipants.Add(participant);
            await _context.SaveChangesAsync();

            return (true, "Etkinliğe başarıyla katıldınız.");
        }

        // --- YENİ EKLENEN ETKİNLİK İPTAL VE MAİL METODU ---

        public async Task<(bool IsSuccess, string Message)> CancelEventAsync(int currentUserId, int eventId)
        {
            // 1. Etkinliği bul
            var targetEvent = await _context.Events
                .Include(e => e.Community)
                .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted);

            if (targetEvent == null)
                return (false, "Etkinlik bulunamadı.");

            if (targetEvent.IsCancelled)
                return (false, "Bu etkinlik zaten iptal edilmiş.");

            // 2. Yetki Kontrolü
            var member = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .FirstOrDefaultAsync(m => m.CommunityId == targetEvent.CommunityId && m.PlatformUserId == currentUserId && !m.IsDeleted);

            if (member == null || !member.CommunityRole.CanCreateEvent)
            {
                return (false, "Bu etkinliği iptal etme yetkiniz bulunmamaktadır.");
            }

            // 3. Etkinliği İptal Et
            targetEvent.IsCancelled = true;

            // 4. Katılımcıları Bul ve Bildirim/E-posta Gönder
            var participants = await _context.EventParticipants
                .Include(ep => ep.PlatformUser)
                .Where(ep => ep.EventId == eventId && !ep.IsDeleted && ep.Status == "Going")
                .ToListAsync();

            if (participants.Any())
            {
                var notifications = new List<Notification>();

                foreach (var participant in participants)
                {
                    // Sistem içi bildirim
                    notifications.Add(new Notification
                    {
                        PlatformUserId = participant.PlatformUserId,
                        Title = "Etkinlik İptal Edildi",
                        Message = $"{targetEvent.Community.Name} tarafından düzenlenen '{targetEvent.Title}' etkinliği iptal edilmiştir.",
                        Type = "System",
                        CreatedAt = DateTime.UtcNow
                    });

                    // E-posta gönderimi (Ana kullanıcıyı bularak mail atıyoruz)
                    try
                    {
                        var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Id == participant.PlatformUser.ExternalUserId);
                        if (mainUser != null)
                        {
                            _emailService.SendEventCancellationEmail(mainUser.Email, targetEvent.Title, targetEvent.Community.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("İptal maili gönderilemedi: " + ex.Message);
                    }
                }

                _context.Notifications.AddRange(notifications);
            }

            await _context.SaveChangesAsync();
            return (true, "Etkinlik iptal edildi ve tüm katılımcılara bilgilendirme e-postası gönderildi.");
        }
    }
}