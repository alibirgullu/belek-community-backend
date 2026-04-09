namespace BelekCommunity.Api.Services
{
    public interface IPushNotificationService
    {
        Task SendPushNotificationAsync(List<string> deviceTokens, string title, string body, object? data = null);
        Task SendPushNotificationToUsersAsync(List<int> userIds, string title, string body, object? data = null);
    }
}
