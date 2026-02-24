using BelekCommunity.Api.Models;
using BelekCommunity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Sadece giriş yapan öğrenciler asistanı kullanabilir
    public class AiChatController : ControllerBase
    {
        private readonly IAiChatService _aiChatService;

        public AiChatController(IAiChatService aiChatService)
        {
            _aiChatService = aiChatService;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AiChatRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
            int currentUserId = int.Parse(userIdString);

            var result = await _aiChatService.SendMessageAsync(currentUserId, request);

            if (!result.IsSuccess)
                return BadRequest(new { Message = result.BotResponse });

            return Ok(new { Response = result.BotResponse });
        }
    }
}