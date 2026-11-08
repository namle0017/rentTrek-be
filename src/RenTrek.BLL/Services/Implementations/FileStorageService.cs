using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

public class FileStorageService : IFileStorageService
{
    private const long MaxFileSizeInBytes = 2 * 1024 * 1024; // 2MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly CloudinarySettings _cloudinarySettings;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IOptions<CloudinarySettings> cloudinaryOptions,
        ILogger<FileStorageService> logger)
    {
        _cloudinarySettings = cloudinaryOptions.Value;
        _logger = logger;
    }

    public async Task<string> SaveAvatarAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (fileLength <= 0)
        {
            throw new ValidationException("File ảnh không được để trống");
        }

        if (fileLength > MaxFileSizeInBytes)
        {
            throw new ValidationException("Dung lượng ảnh tối đa là 2MB");
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ValidationException("Định dạng ảnh không hợp lệ. Chỉ chấp nhận các định dạng .jpg, .jpeg, .png, .webp");
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new ValidationException("Kiểu dữ liệu ảnh (MIME type) không được hỗ trợ");
        }

        // 1. Thử tải lên Cloudinary CDN nếu đã cấu hình
        if (_cloudinarySettings.IsConfigured)
        {
            try
            {
                var account = new CloudinaryDotNet.Account(
                    _cloudinarySettings.CloudName,
                    _cloudinarySettings.ApiKey,
                    _cloudinarySettings.ApiSecret);

                var cloudinary = new Cloudinary(account) { Api = { Secure = true } };

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(originalFileName, fileStream),
                    Folder = "rentrek/avatars",
                    PublicId = $"avatar_{userId:N}",
                    Overwrite = true,
                    Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                };

                var uploadResult = await cloudinary.UploadAsync(uploadParams, cancellationToken);
                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK && uploadResult.SecureUrl != null)
                {
                    _logger.LogInformation("Đã tải avatar lên Cloudinary thành công cho user {UserId}: {Url}", userId, uploadResult.SecureUrl);
                    return uploadResult.SecureUrl.ToString();
                }

                _logger.LogWarning("Tải lên Cloudinary thất bại ({Error}), chuyển về lưu trữ cục bộ wwwroot", uploadResult.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gọi Cloudinary API, tự động fallback về lưu trữ cục bộ wwwroot");
                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }
            }
        }

        // 2. Fallback: Lưu trữ cục bộ trong wwwroot/uploads/avatars
        var wwwrootDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadFolder = Path.Combine(wwwrootDir, "uploads", "avatars");

        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        var uniqueFileName = $"{userId:N}_{Guid.NewGuid():N}{extension}";
        var fullFilePath = Path.Combine(uploadFolder, uniqueFileName);

        using (var destinationStream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        return $"/uploads/avatars/{uniqueFileName}";
    }

    public void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        // Nếu là URL Cloudinary thì bỏ qua việc xóa file cục bộ
        if (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var cleanPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cleanPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // Bỏ qua lỗi xóa file cũ để không gián đoạn luồng chính
        }
    }
}
