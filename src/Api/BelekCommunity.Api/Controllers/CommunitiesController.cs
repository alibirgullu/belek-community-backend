using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization; 

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class CommunitiesController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public CommunitiesController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // HERKES GÖREBİLİR (Sadece sisteme giriş yapmış olmak yeterli)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var communities = await _context.Communities
                                            .OrderByDescending(c => c.CreatedAt)
                                            .ToListAsync();
            return Ok(communities);
        }

        // SADECE SKS (SuperAdmin) YENİ TOPLULUK AÇABİLİR
        [Authorize(Roles = "SuperAdmin")] // Kritik satır!
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

            return CreatedAtAction(nameof(GetAll), new { id = newCommunity.Id }, newCommunity);
        }
    }
}