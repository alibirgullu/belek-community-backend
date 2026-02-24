using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public interface IAiChatService
    {
        Task<(bool IsSuccess, string BotResponse)> SendMessageAsync(int currentUserId, AiChatRequest request);
    }
}