using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Services
{
    public interface IEventService
    {
        Task<IEnumerable<Event>> GetAllEventsAsync();

        
        Task<(bool IsSuccess, string Message, int? EventId)> CreateEventAsync(int currentUserId, CreateEventRequest request);

        Task<(bool IsSuccess, string Message)> ToggleEventParticipationAsync(int currentUserId, int eventId);
        Task<(bool IsSuccess, string Message)> UpdateEventAsync(int currentUserId, int eventId, UpdateEventRequest request);
        Task<(bool IsSuccess, string Message)> CancelEventAsync(int currentUserId, int eventId);
    }
}