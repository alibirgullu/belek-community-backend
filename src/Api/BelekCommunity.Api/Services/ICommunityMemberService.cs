namespace BelekCommunity.Api.Services
{
    public interface ICommunityMemberService
    {
        Task<(bool IsSuccess, string Message)> JoinCommunityAsync(int currentUserId, int communityId);
        Task<object> GetMembersAsync(int communityId);
        Task<(bool IsSuccess, string Message)> RemoveMemberAsync(int currentUserId, int communityId, int platformUserId);


        Task<(bool IsSuccess, string Message, object? Data)> GetPendingMembersAsync(int currentUserId, int communityId);
        Task<(bool IsSuccess, string Message)> RespondToMembershipRequestAsync(int currentUserId, int communityId, int platformUserId, bool isApproved);
        Task<(bool IsSuccess, string Message)> ChangeMemberRoleAsync(int currentUserId, int communityId, int targetPlatformUserId, string newRoleName);

        Task<bool> IsActiveMemberAsync(int communityId, int userId);
        Task<int[]> GetUserActiveCommunityIdsAsync(int userId);
        Task<bool> CanModerateAsync(int communityId, int userId);
    }
}