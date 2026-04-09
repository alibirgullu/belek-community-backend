using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BelekCommunity.Api.Data;
using BelekCommunity.Api.Entities;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using BelekCommunity.Api.Services;
using System.Security.Claims;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CommunitiesController : ControllerBase
    {
        private readonly BelekCommunityDbContext _context;
        private readonly ICommunityService _communityService; 

        public CommunitiesController(BelekCommunityDbContext context, ICommunityService communityService)
        {
            _context = context;
            _communityService = communityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? category)
        {
            bool isSuperAdmin = User.IsInRole("SuperAdmin");

            var query = _context.Communities.Where(c => !c.IsDeleted);

            // Sadece normal öğrenciler için (SuperAdmin değilse) sadece onaylıları getir
            if (!isSuperAdmin)
            {
                query = query.Where(c => c.Status == "Active");
            }

            if (!string.IsNullOrEmpty(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(lowerSearch) || c.Description.ToLower().Contains(lowerSearch));
            }

            if (!string.IsNullOrEmpty(category) && category != "Tümü")
            {
                var lowerCategory = category.ToLower();
                query = query.Where(c => c.Category != null && c.Category.Name.ToLower() == lowerCategory);
            }

            var communities = await query
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Status,
                    c.Description,
                    CategoryId = c.CategoryId,
                    CategoryName = c.Category != null ? c.Category.Name : "Genel",
                    LogoUrl = c.LogoUrl,
                    CoverImageUrl = c.CoverImageUrl,
                    c.CreatedAt,
                    PresidentName = c.Members
                        .Where(m => m.Status == "Active" && (m.CommunityRole.Name == "Başkan" || m.CommunityRole.Name == "Admin"))
                        .Select(m => m.PlatformUser.FirstName + " " + m.PlatformUser.LastName)
                        .FirstOrDefault()
                })
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Ok(communities);
        }

        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.CommunityCategories
                .Where(c => !c.IsDeleted)
                .ToListAsync();

            var defaultCategories = new[] { "Yazılım", "Tasarım", "Spor", "Genel", "Müzik", "Girişimcilik" };
            bool addedNew = false;

            foreach (var catName in defaultCategories)
            {
                if (!categories.Any(c => c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase)))
                {
                    var newCat = new CommunityCategory { Name = catName };
                    _context.CommunityCategories.Add(newCat);
                    categories.Add(newCat);
                    addedNew = true;
                }
            }

            if (addedNew)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(categories.Select(c => new { c.Id, c.Name }).OrderBy(c => c.Id));
        }

        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetails(int id)
        {
            var result = await _communityService.GetCommunityDetailsAsync(id);

            if (result == null)
                return NotFound(new { Message = "Topluluk bulunamadı." });

            return Ok(result);
        }
        

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

            if (!string.IsNullOrEmpty(request.PresidentEmail))
            {
                var mainUser = await _context.MainUsers.FirstOrDefaultAsync(u => u.Email == request.PresidentEmail);
                if (mainUser != null)
                {
                    var platformUser = await _context.Users.FirstOrDefaultAsync(u => u.ExternalUserId == mainUser.Id);
                    if (platformUser != null)
                    {
                        var adminRole = await _context.CommunityRoles.FirstOrDefaultAsync(r => r.Name == "Admin" || r.Name == "Başkan");
                        if (adminRole != null)
                        {
                            var newMember = new CommunityMember
                            {
                                CommunityId = newCommunity.Id,
                                PlatformUserId = platformUser.Id,
                                CommunityRoleId = adminRole.Id,
                                Status = "Active",
                                CreatedAt = DateTime.UtcNow,
                                IsDeleted = false
                            };
                            _context.CommunityMembers.Add(newMember);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }

            return CreatedAtAction(nameof(GetDetails), new { id = newCommunity.Id }, newCommunity);
        }

        // --- TOPLULUK GÜNCELLEME ENDPOINT'İ (Mobile Admin için) ---
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCommunityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Kullanıcı kimliği doğrulanamadı.");
            int currentUserId = int.Parse(userIdString);

            var currentRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            var result = await _communityService.UpdateCommunityAsync(currentUserId, currentRoles, id, request);

            if (!result.IsSuccess)
            {
                if (result.Message.Contains("yetkiniz bulunmamaktadır"))
                    return StatusCode(403, new { Message = result.Message });

                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message });
        }
        // --- ÖĞRENCİ TOPLULUK AÇMA TALEBİ ENDPOINT'İ (Mobil için) ---
        [Authorize]
        [HttpPost("request")]
        public async Task<IActionResult> RequestCommunity([FromBody] StudentCommunityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Kullanıcı kimliği doğrulanamadı.");
            int currentUserId = int.Parse(userIdString);

            var dbCategory = await _context.CommunityCategories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == (request.CategoryName ?? "Genel").ToLower());

            var newCommunity = new Community
            {
                Name = request.Name,
                CategoryId = dbCategory?.Id ?? 1,
                Description = $"{request.Description}\n\n[Sistem Notu] Talep Edilen Topluluk Danışmanı: {request.AdvisorName}",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Communities.Add(newCommunity);
            await _context.SaveChangesAsync();

            var baskanRole = await _context.CommunityRoles.FirstOrDefaultAsync(r => r.Name == "Başkan");
            if (baskanRole != null)
            {
                var newMember = new CommunityMember
                {
                    CommunityId = newCommunity.Id,
                    PlatformUserId = currentUserId,
                    CommunityRoleId = baskanRole.Id,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                _context.CommunityMembers.Add(newMember);
                await _context.SaveChangesAsync();
            }

            var superAdmins = await _context.MainUsers
                .Where(u => u.UserType == "SuperAdmin" || u.UserType == "Admin")
                .ToListAsync();

            var platformSuperAdmins = await _context.Users
                .Where(u => superAdmins.Select(sa => sa.Id).Contains(u.ExternalUserId))
                .ToListAsync();

            foreach (var superAdmin in platformSuperAdmins)
            {
                _context.Notifications.Add(new Notification
                {
                    PlatformUserId = superAdmin.Id,
                    Title = "Yeni Topluluk Başvurusu",
                    Message = $"{request.Name} adlı yeni bir topluluk başvurusu var. İncelemek için Yönetim Paneline gidin.",
                    Type = "System",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            if(platformSuperAdmins.Any())
                await _context.SaveChangesAsync();

            return Ok(new { Message = "Topluluk başvurunuz başarıyla SKS birimine iletildi." });
        }


        // --- TOPLULUK ONAY/RED ENDPOINT'İ (Web Admin için) ---
        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateCommunityStatusRequest request)
        {
            var community = await _context.Communities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (community == null)
                return NotFound(new { Message = "Topluluk bulunamadı." });

            
            await _context.Database.ExecuteSqlRawAsync(
                "CALL belek_student_community.sp_update_community({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                id,
                community.CategoryId,
                community.Name,
                community.Description,
                community.LogoUrl ?? "",
                community.CoverImageUrl ?? "",
                request.Status
            );

            
            var president = await _context.CommunityMembers
                .Include(m => m.CommunityRole)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CommunityId == id && (m.CommunityRole.Name == "Admin" || m.CommunityRole.Name == "Başkan"));

            if (president != null)
            {
                string actionText = request.Status == "Active" ? "onaylandı ve aktifleştirildi" : "reddedildi";
                _context.Notifications.Add(new Notification
                {
                    PlatformUserId = president.PlatformUserId,
                    Title = request.Status == "Active" ? "Topluluk Onaylandı!" : "Topluluk Reddedildi",
                    Message = $"{community.Name} adlı topluluğunuz SKS tarafından {actionText}.",
                    Type = "System",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();
            }

            return Ok(new { Message = $"Topluluk başarıyla {request.Status} yapıldı." });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("{id}/category")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            var community = await _context.Communities.FirstOrDefaultAsync(c => c.Id == id);
            if (community == null)
                return NotFound(new { Message = "Topluluk bulunamadı." });

            community.CategoryId = request.CategoryId;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kategori başarıyla güncellendi." });
        }
    }

    public class UpdateCategoryRequest
    {
        public int CategoryId { get; set; }
    }

    public class UpdateCommunityStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class StudentCommunityRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AdvisorName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
    }
}