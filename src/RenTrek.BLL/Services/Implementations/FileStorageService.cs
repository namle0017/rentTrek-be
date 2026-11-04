using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Lưu ảnh lên Supabase Storage; chưa cấu hình Supabase thì lưu vào wwwroot/uploads để dev.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private const string LocalPrefix = "/uploads/";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    // Một host duy nhất nên dùng chung 1 HttpClient, tránh cạn socket
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly SupabaseStorageSettings _supabase;

    public FileStorageService(IOptions<SupabaseStorageSettings> supabase)
    {
        _supabase = supabase.Value;
    }

    public async Task<string> SaveImageAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength,
        string folder,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        if (fileLength <= 0)
            throw new ValidationException("File ảnh không được để trống");

        if (fileLength > maxBytes)
            throw new ValidationException($"Dung lượng ảnh tối đa là {maxBytes / 1024 / 1024}MB");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new ValidationException("Định dạng ảnh không hợp lệ. Chỉ chấp nhận các định dạng .jpg, .jpeg, .png, .webp");

        if (!AllowedContentTypes.Contains(contentType))
            throw new ValidationException("Kiểu dữ liệu ảnh (MIME type) không được hỗ trợ");

        // Tên file do server sinh, không dùng tên gốc để tránh path traversal
        var objectPath = $"{folder}/{Guid.NewGuid():N}{extension}";

        return _supabase.IsConfigured
            ? await UploadToSupabaseAsync(fileStream, contentType, objectPath, cancellationToken)
            : await SaveLocalAsync(fileStream, objectPath, cancellationToken);
    }

    public async Task DeleteImageAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            if (url.StartsWith(LocalPrefix))
            {
                var uploadsRoot = Path.GetFullPath(Path.Combine(WwwRoot, "uploads")) + Path.DirectorySeparatorChar;
                var fullPath = Path.GetFullPath(Path.Combine(WwwRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                if (fullPath.StartsWith(uploadsRoot) && File.Exists(fullPath)) File.Delete(fullPath);
                return;
            }

            if (_supabase.IsConfigured && url.StartsWith(PublicBaseUrl))
            {
                using var request = SupabaseRequest(HttpMethod.Delete, url[PublicBaseUrl.Length..]);
                using var _ = await Http.SendAsync(request, cancellationToken);
            }
        }
        catch
        {
            // Bỏ qua lỗi xóa file cũ để không gián đoạn luồng chính
        }
    }

    private static string WwwRoot => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

    private string PublicBaseUrl => $"{_supabase.Url.TrimEnd('/')}/storage/v1/object/public/{_supabase.Bucket}/";

    private HttpRequestMessage SupabaseRequest(HttpMethod method, string objectPath)
    {
        var request = new HttpRequestMessage(
            method, $"{_supabase.Url.TrimEnd('/')}/storage/v1/object/{_supabase.Bucket}/{objectPath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabase.ServiceKey);
        request.Headers.Add("apikey", _supabase.ServiceKey);
        return request;
    }

    private async Task<string> UploadToSupabaseAsync(
        Stream fileStream, string contentType, string objectPath, CancellationToken cancellationToken)
    {
        using var request = SupabaseRequest(HttpMethod.Post, objectPath);
        request.Content = new StreamContent(fileStream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Supabase Storage trả {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");

        return PublicBaseUrl + objectPath;
    }

    private static async Task<string> SaveLocalAsync(Stream fileStream, string objectPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(WwwRoot, "uploads", objectPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(destination, cancellationToken);

        return LocalPrefix + objectPath;
    }
}
