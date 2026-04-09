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
    }
}
