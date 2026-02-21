using BelekCommunity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BelekCommunity.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Sadece giriş yapanlar resim yükleyebilir
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
                // folder parametresi: "events", "profiles", "communities" olabilir
                var imageUrl = await _fileService.UploadImageAsync(file, folder);

                if (string.IsNullOrEmpty(imageUrl))
                    return BadRequest("Dosya yüklenemedi.");

                // React Native'e resmi döndük!
                return Ok(new { Url = imageUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}