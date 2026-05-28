using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public class EventParticipantDto
    {
        public int PlatformUserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool CheckedIn { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EventFeedbackItemDto
    {
        public int Id { get; set; }
        public int PlatformUserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EventFeedbackReportDto
    {
        public double AverageRating { get; set; }
        public int Count { get; set; }
        public List<EventFeedbackItemDto> Items { get; set; } = new();
    }

    public interface IEventService
    {
        Task<IEnumerable<Event>> GetAllEventsAsync();

        Task<(bool IsSuccess, string Message, int? EventId)> CreateEventAsync(int currentUserId, CreateEventRequest request);

        Task<(bool IsSuccess, string Message)> ToggleEventParticipationAsync(int currentUserId, int eventId);
        Task<(bool IsSuccess, string Message)> UpdateEventAsync(int currentUserId, int eventId, UpdateEventRequest request);
        Task<(bool IsSuccess, string Message)> CancelEventAsync(int currentUserId, int eventId);

        Task<List<EventParticipantDto>> GetParticipantsAsync(int eventId);
        Task<EventFeedbackReportDto> GetFeedbackReportAsync(int eventId);
    }
}
