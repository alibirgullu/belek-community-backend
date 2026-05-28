using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BelekCommunity.Api.Services
{
    public class SystemLogService : ISystemLogService
    {
        private readonly BelekCommunityDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SystemLogService(BelekCommunityDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string? details = null, int? platformUserId = null, string? ipAddress = null)
        {
            try
            {
                var ctx = _httpContextAccessor.HttpContext;

                if (platformUserId == null && ctx?.User != null)
                {
                    var idStr = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(idStr, out var uid)) platformUserId = uid;
                }

                if (string.IsNullOrEmpty(ipAddress) && ctx != null)
                {
                    ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
                }

                var log = new SystemLog
                {
                    Action = action,
                    Details = details,
                    PlatformUserId = platformUserId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.SystemLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // log yazma sessizce başarısız olabilir — ana akışı bloklamamalı
            }
        }
    }
}
