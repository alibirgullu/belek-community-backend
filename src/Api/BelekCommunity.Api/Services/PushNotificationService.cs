using System.Text;
using System.Text.Json;
using BelekCommunity.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly BelekCommunityDbContext _context;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(HttpClient httpClient, BelekCommunityDbContext context, ILogger<PushNotificationService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://exp.host/--/api/v2/");
        }

        public async Task SendPushNotificationAsync(List<string> deviceTokens, string title, string body, object? data = null)
        {
            var validTokens = deviceTokens.Where(t => t.StartsWith("ExponentPushToken[") || t.StartsWith("ExpoPushToken[")).ToList();

            if (!validTokens.Any()) return;

            var payload = validTokens.Select(token => new
            {
                to = token,
                sound = "default",
                title = title,
                body = body,
                data = data
            });

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("push/send", content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to send push notification. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending Expo Push Notification");
            }
        }

        public async Task SendPushNotificationToUsersAsync(List<int> userIds, string title, string body, object? data = null)
        {
            var tokens = await _context.UserDevices
                .Where(d => userIds.Contains(d.PlatformUserId) && d.IsActive && !string.IsNullOrEmpty(d.DeviceToken))
                .Select(d => d.DeviceToken)
                .Distinct()
                .ToListAsync();

            if (tokens.Any())
            {
                await SendPushNotificationAsync(tokens, title, body, data);
            }
        }
    }
}
