using BelekCommunity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] 
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string folder = "general")
        {
            try
            {
                var imageUrl = await _fileService.UploadImageAsync(file, folder);
                if (string.IsNullOrEmpty(imageUrl))
                    return BadRequest("Dosya yüklenemedi.");
                return Ok(new { Url = imageUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> List([FromQuery] string? folder = null, [FromQuery] int maxResults = 50, [FromQuery] string? nextCursor = null)
        {
            try
            {
                var result = await _fileService.ListAsync(folder, maxResults, nextCursor);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpDelete]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Delete([FromQuery] string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return BadRequest(new { Message = "publicId zorunlu." });
            try
            {
                var ok = await _fileService.DeleteAsync(publicId);
                if (!ok) return BadRequest(new { Message = "Silinemedi." });
                return Ok(new { Message = "Dosya silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}