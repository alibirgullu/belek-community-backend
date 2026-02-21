using Microsoft.AspNetCore.Mvc;
using BelekCommunity.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BelekCommunity.Api.Services;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/communities/{communityId}/announcements")]
    [ApiController]
    [Authorize]
    public class AnnouncementsController : ControllerBase
    {
        private readonly IAnnouncementService _announcementService;

        public AnnouncementsController(IAnnouncementService announcementService)
        {
            _announcementService = announcementService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAnnouncements(int communityId)
        {
            var announcements = await _announcementService.GetAnnouncementsAsync(communityId);
            return Ok(announcements);
        }

        [HttpPost]
        public async Task<IActionResult> Create(int communityId, [FromBody] CreateAnnouncementRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var result = await _announcementService.CreateAnnouncementAsync(currentUserId, communityId, request);

            if (!result.IsSuccess)
            {
                if (result.Message.Contains("yetkiniz bulunmamaktadır"))
                    return StatusCode(403, new { Message = result.Message });

                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message, Id = result.AnnouncementId });
        }
    }
}