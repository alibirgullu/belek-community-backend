using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public EventsController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // 1. Tüm Etkinlikleri Listele (GET api/events)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Include(e => e.Community) ile etkinliğin sahibini de getiriyoruz (JOIN işlemi)
            var events = await _context.Events
                                       .Include(e => e.Community)
                                       .OrderByDescending(e => e.StartDate)
                                       .ToListAsync();
            return Ok(events);
        }

        // 2. Yeni Etkinlik Oluştur (POST api/events)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Önce topluluk var mı diye kontrol et
            var community = await _context.Communities.FindAsync(request.CommunityId);
            if (community == null)
                return NotFound("Belirtilen ID'ye sahip topluluk bulunamadı.");

            var newEvent = new Event
            {
                CommunityId = request.CommunityId,
                Title = request.Title,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Location = request.Location,
                PosterUrl = request.PosterUrl,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Etkinlik oluşturuldu", eventId = newEvent.Id });
        }
    }
}