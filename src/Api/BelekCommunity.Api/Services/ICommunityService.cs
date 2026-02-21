using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public interface ICommunityService
    {
        Task<CommunityDetailResponse?> GetCommunityDetailsAsync(int communityId);
    }
}