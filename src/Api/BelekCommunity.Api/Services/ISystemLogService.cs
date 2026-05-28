namespace BelekCommunity.Api.Services
{
    public interface ISystemLogService
    {
        Task LogAsync(string action, string? details = null, int? platformUserId = null, string? ipAddress = null);
    }
}
