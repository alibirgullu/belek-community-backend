using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace BelekCommunity.Api.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly BelekCommunityDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public AiChatService(BelekCommunityDbContext context, IConfiguration configuration, HttpClient httpClient)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<(bool IsSuccess, string BotResponse)> SendMessageAsync(int currentUserId, AiChatRequest request)
        {
            var apiKey = _configuration["GeminiSettings:ApiKey"];
            if (string.IsNullOrEmpty(apiKey)) return (false, "Yapay zeka API anahtarı eksik.");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

            // --- YENİ: VERİTABANINDAN DETAYLI VE CANLI VERİ ÇEKME ---

            // 1. Öğrencinin adını bulalım
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            var studentName = currentUser != null ? currentUser.FirstName : "Öğrenci";

            // 2. Sistemdeki aktif toplulukların sadece isimlerini yan yana virgülle dizelim
            var communities = await _context.Communities
                .Where(c => !c.IsDeleted)
                .Select(c => c.Name)
                .ToListAsync();
            var communityListString = communities.Any() ? string.Join(", ", communities) : "Şu an kayıtlı topluluk yok.";

            // 3. Yaklaşan ilk 10 etkinliği (Tarih, Yer, Hangi Topluluk) detaylıca çekelim
            var upcomingEvents = await _context.Events
                .Include(e => e.Community)
                .Where(e => !e.IsDeleted && !e.IsCancelled && e.StartDate >= DateTime.UtcNow)
                .OrderBy(e => e.StartDate)
                .Take(10) // Token patlamaması için sınır koyuyoruz
                .Select(e => $"- {e.Title} ({e.Community.Name} tarafından, Tarih: {e.StartDate.ToString("dd.MM.yyyy HH:mm")}, Yer: {e.Location ?? "Belirtilmedi"})")
                .ToListAsync();

            var eventsString = upcomingEvents.Any()
                ? string.Join("\n", upcomingEvents)
                : "Şu an planlanmış yaklaşan bir etkinlik bulunmuyor.";
            // -----------------------------------------------------------

            // System Instruction'ı devasa bir bilgi havuzuna çevirdik
            var systemInstruction = $@"Sen Belek Üniversitesi Öğrenci Toplulukları platformunun resmi yapay zeka asistanısın. Adın 'Belek AI'. 
            Sadece üniversitedeki topluluklar, etkinlikler, üyelik süreçleri ve kampüs yaşamı hakkında bilgi verirsin. Öğrencilerle senli benli, dostane, enerjik ve kısa/öz bir dille konuş.
            
            GÜNCEL SİSTEM BİLGİLERİ (Kullanıcıya cevap verirken mutlaka aşağıdaki GERÇEK VERİLERİ kullan. Eğer kullanıcı sana listede olmayan bir topluluk veya etkinlik sorarsa, 'Şu an sistemde böyle bir kayıt yok' de):
            
            - Seninle konuşan öğrencinin adı: {studentName}
            - Okulumuzdaki Aktif Topluluklar: {communityListString}
            
            - Yaklaşan Etkinlikler Listesi:
            {eventsString}";

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemInstruction } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = request.Message } }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Google API Hatası: {responseString}");
                }

                var jsonDocument = JsonDocument.Parse(responseString);
                var botMessage = jsonDocument.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString() ?? "Üzgünüm, sorunu tam anlayamadım.";

                var chatLog = new AiChatLog
                {
                    PlatformUserId = currentUserId,
                    UserMessage = request.Message,
                    BotResponse = botMessage,
                    Intent = "general_chat",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.AiChatLogs.Add(chatLog);
                await _context.SaveChangesAsync();

                return (true, botMessage);
            }
            catch (Exception ex)
            {
                return (false, $"Sistemsel Hata: {ex.Message}");
            }
        }
    }
}