using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BelekCommunity.Api.Services;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/communities/{communityId}/members")]
    [ApiController]
    [Authorize]
    public class CommunityMembersController : ControllerBase
    {
        private readonly ICommunityMemberService _memberService;

        public CommunityMembersController(ICommunityMemberService memberService)
        {
            _memberService = memberService;
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinCommunity(int communityId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _memberService.JoinCommunityAsync(currentUserId, communityId);

            if (!result.IsSuccess) return BadRequest(new { Message = result.Message });
            return Ok(new { Message = result.Message });
        }

        [HttpGet]
        public async Task<IActionResult> GetMembers(int communityId)
        {
            var members = await _memberService.GetMembersAsync(communityId);
            return Ok(members);
        }

        [HttpDelete("{platformUserId}")]
        public async Task<IActionResult> RemoveMember(int communityId, int platformUserId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _memberService.RemoveMemberAsync(currentUserId, communityId, platformUserId);

            if (!result.IsSuccess) return StatusCode(403, new { Message = result.Message });
            return Ok(new { Message = result.Message });
        }

        // --- YENİ: BEKLEYENLERİ GETİR (Sadece Yöneticiler) ---
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingMembers(int communityId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _memberService.GetPendingMembersAsync(currentUserId, communityId);

            if (!result.IsSuccess) return StatusCode(403, new { Message = result.Message });
            return Ok(result.Data);
        }

        // --- YENİ: ÜYEYİ ONAYLA ---
        [HttpPut("{platformUserId}/approve")]
        public async Task<IActionResult> ApproveMember(int communityId, int platformUserId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _memberService.RespondToMembershipRequestAsync(currentUserId, communityId, platformUserId, true);

            if (!result.IsSuccess) return BadRequest(new { Message = result.Message });
            return Ok(new { Message = result.Message });
        }

        // --- YENİ: ÜYEYİ REDDET ---
        [HttpPut("{platformUserId}/reject")]
        public async Task<IActionResult> RejectMember(int communityId, int platformUserId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _memberService.RespondToMembershipRequestAsync(currentUserId, communityId, platformUserId, false);

            if (!result.IsSuccess) return BadRequest(new { Message = result.Message });
            return Ok(new { Message = result.Message });
        }
    }
}