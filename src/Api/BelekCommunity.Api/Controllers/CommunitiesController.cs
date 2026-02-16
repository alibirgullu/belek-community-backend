using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")] // Tarayıcıda: api/communities
    [ApiController]
    public class CommunitiesController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;

        public CommunitiesController(BelekCommunityDbContext context)
        {
            _context = context;
        }

        // GET: api/communities (Tüm toplulukları listele)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // Veritabanındaki 'communities' tablosundan verileri çeker
            var communities = await _context.Communities
                                            .OrderByDescending(c => c.CreatedAt)
                                            .ToListAsync();
            return Ok(communities);
        }

        // POST: api/communities (Yeni topluluk oluştur)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCommunityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Gelen isteği veritabanı nesnesine çeviriyoruz
            var newCommunity = new Community
            {
                Name = request.Name,
                Description = request.Description,
                LogoUrl = request.LogoUrl,
                CoverImageUrl = request.CoverImageUrl,
                Status = "Pending", // İlk başta onay bekliyor olsun
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Communities.Add(newCommunity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = newCommunity.Id }, newCommunity);
        }
    }
}