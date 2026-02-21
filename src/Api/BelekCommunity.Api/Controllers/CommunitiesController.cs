using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using BelekCommunity.Api.Services;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CommunitiesController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;
        private readonly ICommunityService _communityService; // Servisimizi ekledik

        public CommunitiesController(BelekCommunityDbContext context, ICommunityService communityService)
        {
            _context = context;
            _communityService = communityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var communities = await _context.Communities
                                            .Where(c => !c.IsDeleted)
                                            .OrderByDescending(c => c.CreatedAt)
                                            .ToListAsync();
            return Ok(communities);
        }

        // --- YENİ EKLENEN BABA ENDPOINT ---
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetails(int id)
        {
            var result = await _communityService.GetCommunityDetailsAsync(id);

            if (result == null)
                return NotFound(new { Message = "Topluluk bulunamadı." });

            return Ok(result);
        }
        // ----------------------------------

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCommunityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var newCommunity = new Community
            {
                Name = request.Name,
                Description = request.Description,
                LogoUrl = request.LogoUrl,
                CoverImageUrl = request.CoverImageUrl,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Communities.Add(newCommunity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDetails), new { id = newCommunity.Id }, newCommunity);
        }
    }
}