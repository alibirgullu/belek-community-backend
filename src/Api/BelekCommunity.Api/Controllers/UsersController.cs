using Microsoft.AspNetCore.Mvc;
using BelekCommunity.Api.Models;
using BelekCommunity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BelekCommunity.Api.Data;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Entities;

namespace BelekCommunity.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly BelekCommunityDbContext _context;

        public UsersController(IUserService userService, BelekCommunityDbContext context)
        {
            _userService = userService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.RegisterAsync(request);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message, Email = result.Email });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.VerifyEmailAsync(request);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] CreateUserRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.LoginAsync(request);

            if (!result.IsSuccess)
                return Unauthorized(new { message = result.Message, reason = result.Message });

            return Ok(new
            {
                Token = result.Token,
                UserId = result.UserId,
                FullName = result.FullName,
                ProfileImage = result.ProfileImage
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllUsers()
        {
            
            bool isSuperAdmin = User.IsInRole("SuperAdmin");
            var query = _context.Users.Where(u => !u.IsDeleted);

            if (!isSuperAdmin)
            {
                query = query.Where(u => u.Status == "Active");
            }

            var users = await query
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Status
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var profile = await _userService.GetUserProfileAsync(currentUserId);

            if (profile == null)
                return NotFound(new { Message = "Kullanıcı profili bulunamadı." });

            return Ok(profile);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.ForgotPasswordAsync(request.Email);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.ResetPasswordAsync(request);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message });
        }
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var result = await _userService.UpdateProfileAsync(currentUserId, request);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message });
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.UpdateUserStatusAsync(id, request.Status);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message });
        }

        [HttpPost("devices")]
        [Authorize]
        public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceToken)) return BadRequest("Token is required.");

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int currentUserId = int.Parse(userIdString);

            // Başka bir kullanıcının bu tokene sahip olmasını engelle (Eğer telefon el değiştirdiyse veya farklı bir hesaba geçildiyse)
            var existingOtherUserDevices = await _context.UserDevices
                .Where(d => d.DeviceToken == request.DeviceToken && d.PlatformUserId != currentUserId)
                .ToListAsync();

            if (existingOtherUserDevices.Any())
            {
                _context.UserDevices.RemoveRange(existingOtherUserDevices);
            }

            // Mevcut kullanıcı için bu cihaz daha önce kaydedilmiş mi?
            var myDevice = await _context.UserDevices
                .FirstOrDefaultAsync(d => d.DeviceToken == request.DeviceToken && d.PlatformUserId == currentUserId);

            if (myDevice != null)
            {
                // Varsa güncelle
                myDevice.DeviceType = request.DeviceType;
                myDevice.DeviceName = request.DeviceName;
                myDevice.LastActiveAt = DateTime.UtcNow;
                myDevice.IsActive = true;
            }
            else
            {
                // Yoksa yeni oluştur
                var newDevice = new UserDevice
                {
                    PlatformUserId = currentUserId,
                    DeviceToken = request.DeviceToken,
                    DeviceType = request.DeviceType,
                    DeviceName = request.DeviceName,
                    LastActiveAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.UserDevices.AddAsync(newDevice);
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Device push token registered successfully." });
        }
    }

    public class UpdateUserStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class DeviceRegistrationRequest
    {
        public string DeviceToken { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? DeviceName { get; set; }
    }
}