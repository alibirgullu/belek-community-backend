using Microsoft.AspNetCore.Mvc;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Models;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public UsersController(BelekCommunityDbContext context, IConfiguration configuration, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // 1. REGISTER (Kayıt Ol)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Veritabanında bu mail var mı diye bakıyoruz
            var existingUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            // SENARYO A: Kullanıcı Zaten Var
            if (existingUser != null)
            {
                // Zaten doğrulanmışsa hata dön
                if (existingUser.IsEmailVerified)
                {
                    return BadRequest("Bu e-posta zaten kullanımda. Şifrenizi unuttuysanız giriş ekranından sıfırlayabilirsiniz.");
                }

                // A2: Yarım kalan kayıt (Doğrulanmamış) -> Bilgileri Güncelle ve Yeni Kod Gönder
                var newCode = Random.Shared.Next(100000, 999999).ToString();
                var newExpires = DateTime.UtcNow.AddMinutes(3);

                // --- GÜNCEL ÇÖZÜM ---
                // PostgreSQL Stored Procedure çağırıyoruz.
                // ::timestamp -> Tarih formatı hatasını çözer.
                // CAST(NULL AS text) -> Bilinmeyen tip hatasını çözer.
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT public.update_user_full_profile({0}, {1}, {2}::timestamp, {3}, {4}, {5}, CAST(NULL AS text))",
                    request.Email,     // {0}
                    newCode,           // {1}
                    newExpires,        // {2}
                    request.Password,  // {3}
                    request.FirstName, // {4}
                    request.LastName   // {5}
                );
                // --------------------

                // Mail Gönder
                try
                {
                    _emailService.SendVerificationCode(request.Email, newCode);
                }
                catch (Exception ex) { Console.WriteLine("Mail hatası: " + ex.Message); }

                return Ok(new { Message = "Yarım kalan kaydınız güncellendi. Yeni doğrulama kodu gönderildi.", Email = request.Email });
            }

            // SENARYO B: Kullanıcı Hiç Yok (Sıfırdan Kayıt)
            var code = Random.Shared.Next(100000, 999999).ToString();

            // ID Hesaplama (Auto Increment olmadığı için)
            int nextId = 1;
            if (await _context.MainUsers.AnyAsync())
            {
                nextId = await _context.MainUsers.MaxAsync(u => u.Id) + 1;
            }

            var newMainUser = new MainUser
            {
                Id = nextId,
                Username = request.Email, // Zorunlu alan
                Email = request.Email,
                PasswordHash = request.Password,
                FirstName = request.FirstName,
                LastName = request.LastName,
                UserType = request.UserType,
                IsActive = false,
                IsEmailVerified = false,
                PasswordResetToken = code,
                PasswordResetExpires = DateTime.UtcNow.AddMinutes(3),
                CreateDate = DateTime.UtcNow,
                UpdateDate = DateTime.UtcNow // Zorunlu alan
            };

            _context.MainUsers.Add(newMainUser);
            // Insert işleminde yetki sorunu olmadığı için SaveChanges kullanabiliyoruz
            await _context.SaveChangesAsync();

            try
            {
                _emailService.SendVerificationCode(request.Email, code);
            }
            catch (Exception ex) { Console.WriteLine("Mail hatası: " + ex.Message); }

            return Ok(new { Message = "Kayıt başarılı. Doğrulama kodu e-postanıza gönderildi.", Email = request.Email });
        }

        // 2. VERIFY (E-posta Doğrulama)
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            // Önce sadece okuma yapıyoruz (Yetki istemez)
            var user = await _context.MainUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            // Kod Kontrolü
            if (user.PasswordResetToken != request.Code)
                return BadRequest("Girdiğiniz kod hatalı.");

            // Süre Kontrolü
            if (user.PasswordResetExpires < DateTime.UtcNow)
                return BadRequest("Kodun süresi dolmuş. Lütfen tekrar kayıt olun.");

            // --- DEĞİŞİKLİK BURADA ---
            // 'user' nesnesini C# tarafında güncelleyip SaveChanges yaparsak 'Permission Denied' alırız.
            // Bunun yerine DBA'in izin verdiği fonksiyonu çağırıyoruz:

            await _context.Database.ExecuteSqlRawAsync(
                "SELECT public.verify_user_account({0})",
                request.Email
            );
            // -------------------------

            // Platform Profilini (Senin Şeman) Oluştur
            // (Senin şemanda INSERT yetkin olduğu için burada sorun çıkmaz)
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
                await _context.SaveChangesAsync(); // Bu sadece platform_users tablosunu etkiler
            }

            return Ok(new { Message = "E-posta başarıyla doğrulandı. Artık giriş yapabilirsiniz." });
        }

        // 3. LOGIN (Giriş Yap)
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] CreateUserRequest request)
        {
            var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (mainUser == null || mainUser.PasswordHash != request.Password)
                return Unauthorized("E-posta veya şifre hatalı.");

            if (!mainUser.IsEmailVerified)
                return Unauthorized("Giriş yapmadan önce lütfen e-posta adresinizi doğrulayın.");

            var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == mainUser.Id);

            // Eğer verify adımında bir hata olduysa ve profil oluşmadıysa burada oluştur (Yedek)
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

            // JWT Token Üretimi
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

            return Ok(new
            {
                Token = tokenString,
                UserId = platformUser.Id,
                FullName = $"{platformUser.FirstName} {platformUser.LastName}",
                ProfileImage = platformUser.ProfileImageUrl
            });
        }
    }
}