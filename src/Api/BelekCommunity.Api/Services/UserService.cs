using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
            int nextId = 1;
            if (await _context.MainUsers.AnyAsync())
            {
                nextId = await _context.MainUsers.MaxAsync(u => u.Id) + 1;
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var newMainUser = new MainUser
            {
                Id = nextId,
                Username = request.Email,
                Email = request.Email,
                PasswordHash = hashedPassword,
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserType = request.UserType,
                IsActive = false,
                IsEmailVerified = false,
                PasswordResetToken = code,
                PasswordResetExpires = DateTime.UtcNow.AddMinutes(3),
                CreateDate = DateTime.UtcNow,
                UpdateDate = DateTime.UtcNow
            };

            _context.MainUsers.Add(newMainUser);
            await _context.SaveChangesAsync();

            try { _emailService.SendVerificationCode(request.Email, code); }
            catch (Exception ex) { Console.WriteLine("Mail hatası: " + ex.Message); }

            return (true, "Kayıt başarılı. Doğrulama kodu e-postanıza gönderildi.", request.Email);
        }

        public async Task<(bool IsSuccess, string Message)> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _context.MainUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null) return (false, "Kullanıcı bulunamadı.");
            if (user.PasswordResetToken != request.Code) return (false, "Girdiğiniz kod hatalı.");
            if (user.PasswordResetExpires < DateTime.UtcNow) return (false, "Kodun süresi dolmuş. Lütfen tekrar kayıt olun.");

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

        public async Task<(bool IsSuccess, string Message, string? Token, int? UserId, string? FullName, string? ProfileImage)> LoginAsync(CreateUserRequest request)
        {
            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (mainUser == null || !BCrypt.Net.BCrypt.Verify(request.Password, mainUser.PasswordHash))
                return (false, "E-posta veya şifre hatalı.", null, null, null, null);

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
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:DurationInMinutes"]!)),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return (true, "Giriş başarılı", tokenString, platformUser.Id, $"{platformUser.FirstName} {platformUser.LastName}", platformUser.ProfileImageUrl);
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
                .Where(m => m.PlatformUserId == platformUserId && !m.IsDeleted && !m.Community.IsDeleted)
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
                    CommunityName = ep.Event.Community.Name,
                    StartDate = ep.Event.StartDate,
                    Location = ep.Event.Location,
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

            if (request.Biography != null)
            {
                var userDetail = await _context.PlatformUserDetails
                    .FirstOrDefaultAsync(d => d.PlatformUserId == platformUserId && !d.IsDeleted);

                if (userDetail == null)
                {
                    userDetail = new PlatformUserDetail
                    {
                        PlatformUserId = platformUserId,
                        Biography = request.Biography,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    _context.PlatformUserDetails.Add(userDetail);
                }
                else
                {
                    userDetail.Biography = request.Biography;
                    userDetail.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return (true, "Profiliniz başarıyla güncellendi.");
        }

        // --- YENİ ŞİFRE SIFIRLAMA METOTLARI (DBA UYUMLU, SADECE INSERT YAPAN) ---
        public async Task<(bool IsSuccess, string Message)> ForgotPasswordAsync(string email)
        {
            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (mainUser == null) return (false, "Bu e-posta adresine kayıtlı bir hesap bulunamadı.");

            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == mainUser.Id);
            if (platformUser == null) return (false, "Kullanıcı profilinizde bir hata var.");

            var resetCode = Random.Shared.Next(100000, 999999).ToString();

            // DBA'in izni olmadığı için ana tabloyu güncellemek (UPDATE) yerine, 
            // sadece yeni tabloya kayıt atıyoruz (INSERT).
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

            // DİKKAT: UPDATE işlemi DBA kısıtlamasına takıldığı için, şifre güncellemeyi
            // veritabanındaki özel fonksiyon ile (Stored Procedure/Function) yapıyoruz.
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT public.reset_user_password({0}, {1}, {2})",
                request.Email, hashedPassword, activeToken.Id
            );

            return (true, "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.");
        }
    }
}