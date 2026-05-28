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

        [HttpPut("{eventId}")]
        [Authorize]
        public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] UpdateEventRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int currentUserId = int.Parse(userIdString);

            var result = await _eventService.UpdateEventAsync(currentUserId, eventId, request);

            if (!result.IsSuccess)
            {
                if (result.Message.Contains("yetkiniz bulunmamaktadır"))
                    return StatusCode(403, new { Message = result.Message });

                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message });
        }
        
        [HttpGet("{eventId}/participants")]
        [Authorize]
        public async Task<IActionResult> GetParticipants(int eventId)
        {
            var participants = await _eventService.GetParticipantsAsync(eventId);
            return Ok(participants);
        }

        [HttpGet("{eventId}/feedback")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> GetFeedbackReport(int eventId)
        {
            var report = await _eventService.GetFeedbackReportAsync(eventId);
            return Ok(report);
        }

        [HttpPut("{eventId}/cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel(int eventId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var result = await _eventService.CancelEventAsync(currentUserId, eventId);

            if (!result.IsSuccess)
            {
                if (result.Message.Contains("yetkiniz bulunmamaktadır"))
                    return StatusCode(403, new { Message = result.Message });

                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message });
        }
    }
}