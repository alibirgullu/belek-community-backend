using Microsoft.AspNetCore.Http;

namespace BelekCommunity.Api.Services
{
    public class CloudinaryFileItem
    {
        public string PublicId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Folder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CloudinaryListResult
    {
        public List<CloudinaryFileItem> Items { get; set; } = new();
        public string? NextCursor { get; set; }
    }

    public interface IFileService
    {
        Task<string?> UploadImageAsync(IFormFile file, string folderName);
        Task<CloudinaryListResult> ListAsync(string? folder, int maxResults, string? nextCursor);
        Task<bool> DeleteAsync(string publicId);
    }
}
