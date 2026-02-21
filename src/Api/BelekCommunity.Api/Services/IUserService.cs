using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public interface IUserService
    {
        Task<(bool IsSuccess, string Message, string? Email)> RegisterAsync(RegisterRequest request);
        Task<(bool IsSuccess, string Message)> VerifyEmailAsync(VerifyEmailRequest request);
        Task<UserProfileResponse?> GetUserProfileAsync(int platformUserId);
        // Giriş başarılı olursa Token ve kullanıcı bilgilerini döneceğiz
        Task<(bool IsSuccess, string Message, string? Token, int? UserId, string? FullName, string? ProfileImage)> LoginAsync(CreateUserRequest request);
    }
}