using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public interface IAnnouncementService
    {
        Task<object> GetAnnouncementsAsync(int communityId);
        Task<(bool IsSuccess, string Message, int? AnnouncementId)> CreateAnnouncementAsync(int currentUserId, int communityId, CreateAnnouncementRequest request);
    }
}