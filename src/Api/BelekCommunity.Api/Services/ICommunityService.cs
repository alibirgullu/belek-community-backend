using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public interface ICommunityService
    {
        Task<CommunityDetailResponse?> GetCommunityDetailsAsync(int communityId);
        Task<(bool IsSuccess, string Message)> UpdateCommunityAsync(int currentUserId, IList<string> currentRoles, int communityId, UpdateCommunityRequest request);
    }
}