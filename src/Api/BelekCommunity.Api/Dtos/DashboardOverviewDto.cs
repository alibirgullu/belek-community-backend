using System;
using System.Collections.Generic;

namespace BelekCommunity.Api.Dtos
{
    public class DashboardOverviewDto
    {
        public int TotalCommunities { get; set; }
        public int ActiveStudents { get; set; }
        public int PendingApprovals { get; set; }
        public string MonthlyGrowth { get; set; } = string.Empty;
        public List<DashboardActivityDto> RecentActivities { get; set; } = new List<DashboardActivityDto>();
    }

    public class DashboardActivityDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
