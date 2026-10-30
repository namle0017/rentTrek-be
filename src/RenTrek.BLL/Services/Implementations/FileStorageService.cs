using RenTrek.BLL.Exceptions;
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
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        return $"/uploads/avatars/{uniqueFileName}";
    }

    public void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

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
