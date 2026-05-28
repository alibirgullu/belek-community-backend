using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace BelekCommunity.Api.Services
{
    public class FileService : IFileService
    {
        private readonly Cloudinary _cloudinary;

        public FileService(IConfiguration config)
        {
            
            var account = new Account(
                config["CloudinarySettings:CloudName"],
                config["CloudinarySettings:ApiKey"],
                config["CloudinarySettings:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
        }

        public async Task<string?> UploadImageAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0) return null;

            
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                throw new Exception("Sadece resim formatları (.jpg, .png, vs.) desteklenmektedir.");

           
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"BelekCommunity/{folderName}", 
                Transformation = new Transformation().Quality("auto").FetchFormat("auto") 
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                throw new Exception(uploadResult.Error.Message);


            return uploadResult.SecureUrl.AbsoluteUri;
        }

        public async Task<CloudinaryListResult> ListAsync(string? folder, int maxResults, string? nextCursor)
        {
            var prefix = string.IsNullOrWhiteSpace(folder)
                ? "BelekCommunity/"
                : $"BelekCommunity/{folder.Trim('/')}/";

            var listParams = new ListResourcesByPrefixParams
            {
                Type = "upload",
                Prefix = prefix,
                MaxResults = Math.Clamp(maxResults, 1, 100),
                NextCursor = nextCursor,
            };

            var result = await _cloudinary.ListResourcesAsync(listParams);

            var items = (result.Resources ?? Array.Empty<Resource>())
                .Select(r =>
                {
                    DateTime createdAt = DateTime.UtcNow;
                    var createdStr = r.CreatedAt;
                    if (!string.IsNullOrEmpty(createdStr) && DateTime.TryParse(createdStr, out var parsed))
                        createdAt = parsed;

                    var url = r.SecureUrl?.ToString() ?? r.Url?.ToString() ?? string.Empty;
                    string? folderOfItem = null;
                    var slashIdx = r.PublicId?.LastIndexOf('/') ?? -1;
                    if (slashIdx > 0) folderOfItem = r.PublicId!.Substring(0, slashIdx);

                    return new CloudinaryFileItem
                    {
                        PublicId = r.PublicId,
                        Url = url,
                        Format = r.Format ?? string.Empty,
                        Bytes = r.Bytes,
                        Width = r.Width,
                        Height = r.Height,
                        Folder = folderOfItem,
                        CreatedAt = createdAt
                    };
                })
                .ToList();

            return new CloudinaryListResult { Items = items, NextCursor = result.NextCursor };
        }

        public async Task<bool> DeleteAsync(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId)) return false;
            var deleteParams = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok" || result.Result == "not found";
        }
    }
}