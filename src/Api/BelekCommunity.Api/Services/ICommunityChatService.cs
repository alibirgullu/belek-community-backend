using BelekCommunity.Api.Dtos;

namespace BelekCommunity.Api.Services
{
    public interface ICommunityChatService
    {
        Task<(bool IsSuccess, string Message, CommunityMessageDto? Data)> SaveMessageAsync(int communityId, int senderId, string content);
        Task<(bool IsSuccess, string Message, object? Data)> GetCommunityMessagesAsync(int communityId, int currentUserId, int page = 1, int pageSize = 50);

        Task<(bool IsSuccess, string Message, List<Guid> MarkedIds)> MarkMessagesAsReadAsync(int communityId, int userId, List<Guid> messageIds);
        Task<int> GetUnreadCountAsync(int communityId, int userId);

        Task<(bool IsSuccess, string Message, CommunityMessageDto? Data)> EditMessageAsync(Guid messageId, int userId, string newContent);
        Task<(bool IsSuccess, string Message, int CommunityId)> DeleteMessageAsync(Guid messageId, int userId);
    }
}
