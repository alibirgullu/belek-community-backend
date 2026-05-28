using BelekCommunity.Api.Data;
using BelekCommunity.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BelekCommunity.Api.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public DashboardController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        [HttpGet("overview")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> GetOverview()
        {
            var totalCommunities = await _context.Communities.CountAsync(c => !c.IsDeleted && c.Status == "Active");
            var pendingApprovals = await _context.Communities.CountAsync(c => !c.IsDeleted && c.Status == "Pending");
            var totalStudents = await _context.Users.CountAsync(u => !u.IsDeleted && u.Status == "Active");

            // Calculate Monthly Growth (Users joined this month vs last month)
            var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastMonthStart = currentMonthStart.AddMonths(-1);

            var currMonthUsers = await _context.Users.CountAsync(u => u.CreatedAt >= currentMonthStart);
            var lastMonthUsers = await _context.Users.CountAsync(u => u.CreatedAt >= lastMonthStart && u.CreatedAt < currentMonthStart);

            string growthStr = "+0%";
            if (lastMonthUsers > 0)
            {
                var growth = ((double)(currMonthUsers - lastMonthUsers) / lastMonthUsers) * 100;
                growthStr = growth >= 0 ? $"+{Math.Round(growth)}%" : $"{Math.Round(growth)}%";
            }
            else if (currMonthUsers > 0)
            {
                growthStr = "+100%";
            }

            var recentActivities = new List<DashboardActivityDto>();

            // Recent Community Applications
            var recentCommunities = await _context.Communities
                .Where(c => !c.IsDeleted && c.Status == "Pending")
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var c in recentCommunities)
            {
                recentActivities.Add(new DashboardActivityDto
                {
                    Title = "Yeni Topluluk Başvurusu",
                    Description = $"{c.Name} topluluğu kurulma talebinde bulundu.",
                    Type = "CommunityApplication",
                    CreatedAt = c.CreatedAt,
                    TimeAgo = GetTimeAgo(c.CreatedAt)
                });
            }

            // Recent Member Joins
            var recentMembers = await _context.CommunityMembers
                .Include(m => m.Community)
                .Include(m => m.PlatformUser)
                .Where(m => !m.IsDeleted && m.Status == "Active")
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var m in recentMembers)
            {
                recentActivities.Add(new DashboardActivityDto
                {
                    Title = "Yeni Üye Katılımı",
                    Description = $"{m.PlatformUser.FirstName} {m.PlatformUser.LastName}, {m.Community.Name} topluluğuna katıldı.",
                    Type = "MemberJoined",
                    CreatedAt = m.CreatedAt,
                    TimeAgo = GetTimeAgo(m.CreatedAt)
                });
            }

            var sortedActivities = recentActivities.OrderByDescending(a => a.CreatedAt).Take(5).ToList();

            var dto = new DashboardOverviewDto
            {
                TotalCommunities = totalCommunities,
                ActiveStudents = totalStudents,
                PendingApprovals = pendingApprovals,
                MonthlyGrowth = growthStr,
                RecentActivities = sortedActivities
            };

            return Ok(dto);
        }

        private string GetTimeAgo(DateTime past)
        {
            var ts = DateTime.UtcNow - past;
            if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes} DAKİKA ÖNCE";
            if (ts.TotalHours < 24) return $"{(int)ts.TotalHours} SAAT ÖNCE";
            if (ts.TotalDays < 30) return $"{(int)ts.TotalDays} GÜN ÖNCE";
            return $"{(int)(ts.TotalDays / 30)} AY ÖNCE";
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> GetStatistics()
        {
            var categoryDistribution = await _context.Communities
                .Where(c => !c.IsDeleted && c.Status == "Active")
                .GroupBy(c => c.Category != null ? c.Category.Name : "Kategorisiz")
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
            var firstOfMonth = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var memberMonthly = await _context.CommunityMembers
                .Where(m => !m.IsDeleted && m.CreatedAt >= firstOfMonth)
                .GroupBy(m => new { m.CreatedAt.Year, m.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var eventMonthly = await _context.Events
                .Where(e => !e.IsDeleted && e.CreatedAt >= firstOfMonth)
                .GroupBy(e => new { e.CreatedAt.Year, e.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var months = new List<object>();
            for (int i = 0; i < 6; i++)
            {
                var dt = firstOfMonth.AddMonths(i);
                var m = memberMonthly.FirstOrDefault(x => x.Year == dt.Year && x.Month == dt.Month)?.Count ?? 0;
                var e = eventMonthly.FirstOrDefault(x => x.Year == dt.Year && x.Month == dt.Month)?.Count ?? 0;
                months.Add(new { Year = dt.Year, Month = dt.Month, Label = dt.ToString("MMM yy"), Members = m, Events = e });
            }

            var topCommunities = await _context.Communities
                .Where(c => !c.IsDeleted && c.Status == "Active")
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    MemberCount = c.Members.Count(m => m.Status == "Active" && !m.IsDeleted),
                    CategoryName = c.Category != null ? c.Category.Name : "Genel"
                })
                .OrderByDescending(c => c.MemberCount)
                .Take(5)
                .ToListAsync();

            var topEvents = await _context.Events
                .Where(e => !e.IsDeleted && !e.IsCancelled)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    CommunityName = e.Community != null ? e.Community.Name : "",
                    ParticipantCount = _context.EventParticipants.Count(p => p.EventId == e.Id && !p.IsDeleted),
                    e.StartDate
                })
                .OrderByDescending(e => e.ParticipantCount)
                .Take(5)
                .ToListAsync();

            return Ok(new
            {
                CategoryDistribution = categoryDistribution,
                MonthlyTrends = months,
                TopCommunities = topCommunities,
                TopEvents = topEvents
            });
        }

        [HttpGet("system-logs")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> GetSystemLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? action = null, [FromQuery] int? userId = null)
        {
            var query = _context.SystemLogs.Where(l => !l.IsDeleted);
            if (!string.IsNullOrEmpty(action)) query = query.Where(l => l.Action == action);
            if (userId.HasValue) query = query.Where(l => l.PlatformUserId == userId.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.PlatformUserId,
                    l.Action,
                    l.Details,
                    l.IpAddress,
                    l.CreatedAt
                })
                .ToListAsync();

            return Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
        }
    }
}
