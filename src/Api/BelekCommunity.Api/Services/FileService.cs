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
            // appsettings.json'dan bilgileri çekip Cloudinary hesabına bağlanıyoruz
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

            // Sadece resimlere izin verelim
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                throw new Exception("Sadece resim formatları (.jpg, .png, vs.) desteklenmektedir.");

            // Dosyayı Cloudinary'ye fırlatıyoruz
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"BelekCommunity/{folderName}", // Cloudinary'de klasörleme yapar (örn: BelekCommunity/events)
                Transformation = new Transformation().Quality("auto").FetchFormat("auto") // Resim boyutunu otomatik optimize eder!
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
                throw new Exception(uploadResult.Error.Message);

            // Başarılıysa, resmin buluttaki güvenli URL'ini (https://...) dön
            return uploadResult.SecureUrl.AbsoluteUri;
        }
    }
}