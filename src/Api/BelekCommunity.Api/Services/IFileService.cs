using Microsoft.AspNetCore.Http;

namespace BelekCommunity.Api.Services
{
    public interface IFileService
    {
        Task<string?> UploadImageAsync(IFormFile file, string folderName);
    }
}