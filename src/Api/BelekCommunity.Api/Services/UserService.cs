using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BelekCommunity.Api.Services
{
    public class UserService : IUserService
    {
        private readonly BelekCommunityDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public UserService(BelekCommunityDbContext context, IConfiguration configuration, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<(bool IsSuccess, string Message, string? Email)> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
            {
                if (existingUser.IsEmailVerified)
                {
                    return (false, "Bu e-posta zaten kullanımda. Şifrenizi unuttuysanız giriş ekranından sıfırlayabilirsiniz.", null);
                }

                var newCode = Random.Shared.Next(100000, 999999).ToString();
                var newExpires = DateTime.UtcNow.AddMinutes(3);
                var hashedPasswordForUpdate = BCrypt.Net.BCrypt.HashPassword(request.Password);

                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT public.update_user_full_profile({0}, {1}, {2}::timestamp, {3}, {4}, {5}, CAST(NULL AS text))",
                    request.Email, newCode, newExpires, hashedPasswordForUpdate, request.FirstName, request.LastName
                );

                try { _emailService.SendVerificationCode(request.Email, newCode); }
                catch (Exception ex) { Console.WriteLine("Mail hatası: " + ex.Message); }

                return (true, "Yarım kalan kaydınız güncellendi. Yeni doğrulama kodu gönderildi.", request.Email);
            }

            var code = Random.Shared.Next(100000, 999999).ToString();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // RLS'i aşmak için veritabanında oluşturulan SECURITY DEFINER fonksiyonunu kullanıyoruz.
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT public.register_new_user({0}, {1}, {2}, {3}, {4}, {5}, {6}::timestamp)",
                request.Email, request.FirstName, request.LastName, request.UserType, hashedPassword, code, DateTime.UtcNow.AddMinutes(3)
            );

            try { _emailService.SendVerificationCode(request.Email, code); }
            catch (Exception ex) { Console.WriteLine("Mail hatası: " + ex.Message); }

            return (true, "Kayıt başarılı. Doğrulama kodu e-postanıza gönderildi.", request.Email);
        }

        public async Task<(bool IsSuccess, string Message)> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _context.MainUsers
                .Include(u => u.UserAuth)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || user.UserAuth == null) return (false, "Kullanıcı bulunamadı.");
            if (user.UserAuth.PasswordResetToken != request.Code) return (false, "Girdiğiniz kod hatalı.");
            if (user.UserAuth.PasswordResetExpires < DateTime.UtcNow) return (false, "Kodun süresi dolmuş. Lütfen tekrar kayıt olun.");

            await _context.Database.ExecuteSqlRawAsync(
                "SELECT public.verify_user_account({0})",
                request.Email
            );

            var existingPlatformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == user.Id);

            if (existingPlatformUser == null)
            {
                var platformUser = new User
                {
                    ExternalUserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                _context.Users.Add(platformUser);
                await _context.SaveChangesAsync();
            }

            return (true, "E-posta başarıyla doğrulandı. Artık giriş yapabilirsiniz.");
        }

        public async Task<(bool IsSuccess, string Message, AuthTokenResponse? Tokens, int? UserId, string? FullName, string? ProfileImage)> LoginAsync(CreateUserRequest request)
        {
            var mainUser = await _context.MainUsers
                .Include(u => u.UserAuth)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (mainUser == null)
                return (false, "E-posta adresi sistemde bulunamadı.", null, null, null, null);

            if (mainUser.UserAuth == null)
                return (false, "Kullanıcının yetkilendirme (UserAuth) kaydı bulunamadı. Veri tabanı taşıması eksik olabilir.", null, null, null, null);

            if (!BCrypt.Net.BCrypt.Verify(request.Password, mainUser.UserAuth.PasswordHash))
                return (false, "Girdiğiniz şifre hatalı.", null, null, null, null);

            if (!mainUser.IsEmailVerified)
                return (false, "Giriş yapmadan önce lütfen e-posta adresinizi doğrulayın.", null, null, null, null);

            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == mainUser.Id);

            if (platformUser == null)
            {
                platformUser = new User
                {
                    ExternalUserId = mainUser.Id,
                    FirstName = mainUser.FirstName,
                    LastName = mainUser.LastName,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(platformUser);
                await _context.SaveChangesAsync();
            }

            if (platformUser.Status == "Suspended" || platformUser.IsDeleted)
                return (false, "Hesabınız sistem yöneticileri tarafından askıya alınmıştır veya silinmiştir.", null, null, null, null);

            var tokens = await IssueTokenPairAsync(platformUser, mainUser);

            return (true, "Giriş başarılı", tokens, platformUser.Id, $"{platformUser.FirstName} {platformUser.LastName}", platformUser.ProfileImageUrl);
        }

        public async Task<(bool IsSuccess, string Message, AuthTokenResponse? Tokens)> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return (false, "Yenileme anahtarı boş olamaz.", null);

            var stored = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (stored == null)
                return (false, "Geçersiz yenileme anahtarı.", null);

            // Replay tespiti: bu token daha önce iptal edilmişse, muhtemelen çalındı —
            // aynı kullanıcının tüm aktif yenileme anahtarlarını da iptal et.
            if (!stored.IsActive || stored.RevokedAt != null)
            {
                var allUserTokens = await _context.UserRefreshTokens
                    .Where(t => t.PlatformUserId == stored.PlatformUserId && t.IsActive)
                    .ToListAsync();

                foreach (var t in allUserTokens)
                {
                    t.IsActive = false;
                    t.RevokedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                return (false, "Yenileme anahtarı yeniden kullanılmış. Tüm oturumlar güvenlik gereği sonlandırıldı.", null);
            }

            if (stored.ExpiresAt < DateTime.UtcNow)
            {
                stored.IsActive = false;
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return (false, "Yenileme anahtarının süresi dolmuş. Lütfen tekrar giriş yapın.", null);
            }

            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == stored.PlatformUserId && !u.IsDeleted);
            if (platformUser == null || platformUser.Status == "Suspended")
            {
                stored.IsActive = false;
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return (false, "Kullanıcı bulunamadı veya askıya alınmış.", null);
            }

            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Id == platformUser.ExternalUserId);
            if (mainUser == null)
                return (false, "Ana kullanıcı kaydı bulunamadı.", null);

            // Rotasyon: eski token'ı iptal et, yenisini üret ve eskisini yenisi ile işaretle.
            var newTokens = await IssueTokenPairAsync(platformUser, mainUser);

            stored.IsActive = false;
            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByToken = newTokens.RefreshToken;
            await _context.SaveChangesAsync();

            return (true, "Token yenilendi.", newTokens);
        }

        public async Task<(bool IsSuccess, string Message)> LogoutAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return (true, "Çıkış yapıldı.");

            var stored = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (stored != null && stored.IsActive)
            {
                stored.IsActive = false;
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return (true, "Çıkış yapıldı.");
        }

        // --- Yardımcı: Access + Refresh çifti üret ---
        private async Task<AuthTokenResponse> IssueTokenPairAsync(User platformUser, MainUser mainUser)
        {
            var accessMinutes = double.Parse(
                _configuration["JwtSettings:AccessTokenDurationInMinutes"]
                ?? _configuration["JwtSettings:DurationInMinutes"]
                ?? "15");
            var refreshDays = double.Parse(_configuration["JwtSettings:RefreshTokenDurationInDays"] ?? "30");

            var accessExpiresAt = DateTime.UtcNow.AddMinutes(accessMinutes);
            var refreshExpiresAt = DateTime.UtcNow.AddDays(refreshDays);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, platformUser.Id.ToString()),
                    new Claim(ClaimTypes.Email, mainUser.Email),
                    new Claim("ExternalId", mainUser.Id.ToString()),
                    new Claim(ClaimTypes.Role, mainUser.UserType)
                }),
                Expires = accessExpiresAt,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"]
            };

            var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
            var refreshToken = GenerateRefreshToken();

            _context.UserRefreshTokens.Add(new UserRefreshToken
            {
                PlatformUserId = platformUser.Id,
                Token = refreshToken,
                ExpiresAt = refreshExpiresAt,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await _context.SaveChangesAsync();

            return new AuthTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessExpiresAt,
                RefreshTokenExpiresAt = refreshExpiresAt
            };
        }

        private static string GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        public async Task<UserProfileResponse?> GetUserProfileAsync(int platformUserId)
        {
            var platformUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == platformUserId && !u.IsDeleted);

            if (platformUser == null) return null;

            var mainUser = await _context.MainUsers
                .FirstOrDefaultAsync(m => m.Id == platformUser.ExternalUserId);

            var userDetail = await _context.PlatformUserDetails
                .FirstOrDefaultAsync(d => d.PlatformUserId == platformUserId && !d.IsDeleted);

            var myCommunities = await _context.CommunityMembers
                .Include(m => m.Community)
                .Include(m => m.CommunityRole)
                .Where(m => m.PlatformUserId == platformUserId && !m.IsDeleted && !m.Community.IsDeleted && m.Status == "Active")
                .Select(m => new UserCommunityDto
                {
                    CommunityId = m.CommunityId,
                    CommunityName = m.Community.Name,
                    LogoUrl = m.Community.LogoUrl,
                    RoleName = m.CommunityRole.Name,
                    Status = m.Status
                })
                .ToListAsync();

            var upcomingEvents = await _context.EventParticipants
                .Include(ep => ep.Event)
                .ThenInclude(e => e.Community)
                .Where(ep => ep.PlatformUserId == platformUserId && !ep.IsDeleted && !ep.Event.IsDeleted && !ep.Event.IsCancelled && ep.Event.StartDate >= DateTime.UtcNow)
                .OrderBy(ep => ep.Event.StartDate)
                .Select(ep => new UserEventDto
                {
                    EventId = ep.EventId,
                    Title = ep.Event.Title,
                    Description = ep.Event.Description,
                    CommunityName = ep.Event.Community.Name,
                    StartDate = ep.Event.StartDate,
                    Location = ep.Event.Location,
                    PosterUrl = ep.Event.PosterUrl,
                    Status = ep.Status
                })
                .ToListAsync();

            return new UserProfileResponse
            {
                Id = platformUser.Id,
                FirstName = platformUser.FirstName,
                LastName = platformUser.LastName,
                Email = mainUser?.Email ?? "",
                ProfileImageUrl = platformUser.ProfileImageUrl,
                Phone = platformUser.Phone,
                Biography = userDetail?.Biography,
                Department = userDetail?.Department,
                MyCommunities = myCommunities,
                UpcomingEvents = upcomingEvents
            };
        }

        // --- PROFİL GÜNCELLEME METODU ---
        public async Task<(bool IsSuccess, string Message)> UpdateProfileAsync(int platformUserId, UpdateProfileRequest request)
        {
            var platformUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == platformUserId && !u.IsDeleted);

            if (platformUser == null)
                return (false, "Kullanıcı bulunamadı.");

            if (request.ProfileImageUrl != null)
                platformUser.ProfileImageUrl = request.ProfileImageUrl;

            if (request.Phone != null)
                platformUser.Phone = request.Phone;

            platformUser.UpdatedAt = DateTime.UtcNow;

            
            if (request.Biography != null || request.Department != null)
            {
                var userDetail = await _context.PlatformUserDetails
                    .FirstOrDefaultAsync(d => d.PlatformUserId == platformUserId && !d.IsDeleted);

                if (userDetail == null)
                {
                    userDetail = new PlatformUserDetail
                    {
                        PlatformUserId = platformUserId,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    
                    if (request.Biography != null) userDetail.Biography = request.Biography;
                    if (request.Department != null) userDetail.Department = request.Department; 

                    _context.PlatformUserDetails.Add(userDetail);
                }
                else
                {
                    
                    if (request.Biography != null) userDetail.Biography = request.Biography;
                    if (request.Department != null) userDetail.Department = request.Department; 

                    userDetail.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return (true, "Profiliniz başarıyla güncellendi.");
        }

        
        public async Task<(bool IsSuccess, string Message)> ForgotPasswordAsync(string email)
        {
            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (mainUser == null) return (false, "Bu e-posta adresine kayıtlı bir hesap bulunamadı.");

            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == mainUser.Id);
            if (platformUser == null) return (false, "Kullanıcı profilinizde bir hata var.");

            var resetCode = Random.Shared.Next(100000, 999999).ToString();

            
            var resetToken = new PasswordResetToken
            {
                PlatformUserId = platformUser.Id,
                Token = resetCode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            try { _emailService.SendVerificationCode(mainUser.Email, resetCode); }
            catch (Exception ex) { Console.WriteLine("Mail hatası: " + ex.Message); }

            return (true, "Şifre sıfırlama kodu e-posta adresinize gönderildi.");
        }

        public async Task<(bool IsSuccess, string Message)> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (mainUser == null) return (false, "Kullanıcı bulunamadı.");

            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == mainUser.Id);
            if (platformUser == null) return (false, "Kullanıcı profil hatası.");

            var activeToken = await _context.PasswordResetTokens
                .Where(t => t.PlatformUserId == platformUser.Id && t.Token == request.Token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (activeToken == null) return (false, "Girdiğiniz kod hatalı veya süresi dolmuş.");

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT public.reset_user_password({0}, {1}, {2})",
                request.Email, hashedPassword, activeToken.Id
            );

            return (true, "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.");
        }

        public async Task<(bool IsSuccess, string Message)> UpdateUserStatusAsync(int platformUserId, string newStatus)
        {
            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == platformUserId);
            if (platformUser == null) return (false, "Kullanıcı bulunamadı.");

            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(m => m.Id == platformUser.ExternalUserId);
            if (mainUser == null) return (false, "Ana kullanıcı kaydı bulunamadı.");

            platformUser.Status = newStatus;
            
            if (newStatus == "Suspended") {
                mainUser.IsActive = false;
            } else if (newStatus == "Active") {
                
                mainUser.IsActive = true;
            }

            platformUser.UpdatedAt = DateTime.UtcNow;
            mainUser.UpdateDate = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return (true, $"Kullanıcı durumu başarıyla '{newStatus}' olarak güncellendi.");
        }
    }
}