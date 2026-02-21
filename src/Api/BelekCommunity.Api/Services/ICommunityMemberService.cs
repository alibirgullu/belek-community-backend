namespace BelekCommunity.Api.Services
{
    public interface ICommunityMemberService
    {
        Task<(bool IsSuccess, string Message)> JoinCommunityAsync(int currentUserId, int communityId);
        Task<object> GetMembersAsync(int communityId);
        Task<(bool IsSuccess, string Message)> RemoveMemberAsync(int currentUserId, int communityId, int platformUserId);

        // --- YENİ EKLENEN ADMİN METOTLARI ---
        Task<(bool IsSuccess, string Message, object? Data)> GetPendingMembersAsync(int currentUserId, int communityId);
        Task<(bool IsSuccess, string Message)> RespondToMembershipRequestAsync(int currentUserId, int communityId, int platformUserId, bool isApproved);
    }
}