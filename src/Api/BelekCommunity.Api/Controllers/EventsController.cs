using Microsoft.AspNetCore.Mvc;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BelekCommunity.Api.Services;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventsController(IEventService eventService)
        {
            _eventService = eventService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var events = await _eventService.GetAllEventsAsync();
            return Ok(events);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Kullanıcı kimliği doğrulanamadı.");
            int currentUserId = int.Parse(userIdString);

            var result = await _eventService.CreateEventAsync(currentUserId, request);

            if (!result.IsSuccess)
            {
                if (result.Message.Contains("yetkiniz bulunmamaktadır"))
                    return StatusCode(403, new { Message = result.Message });

                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message, EventId = result.EventId });
        }

        [HttpPost("{eventId}/participate")]
        [Authorize]
        public async Task<IActionResult> Participate(int eventId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var result = await _eventService.ToggleEventParticipationAsync(currentUserId, eventId);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.Message });

            return Ok(new { Message = result.Message });
        }
    }
}