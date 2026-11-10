using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>Signed server-side uploads; Cloudinary credentials never leave the backend.</summary>
public sealed class ProductImageUploadService(IOptions<CloudinarySettings> options, HttpClient? http = null) : IProductImageUploadService
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(45) };

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
    {
        if (length is <= 0 or > IProductImageUploadService.MaxBytes)
            throw new ValidationException("Vui lòng chọn ảnh có dung lượng từ 1 byte đến 5MB");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var validType = (extension is ".jpg" or ".jpeg" && contentType == "image/jpeg")
            || (extension == ".png" && contentType == "image/png")
            || (extension == ".webp" && contentType == "image/webp");
        if (!validType)
            throw new ValidationException("Chỉ chấp nhận ảnh JPG, PNG hoặc WebP");
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new ValidationException("Cloudinary chưa được cấu hình đầy đủ trên máy chủ");

        // Cloudinary validates timestamps against real time, even when the app's dev clock is simulated.
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var publicId = $"rentrek/products/{Guid.NewGuid():N}";
        var parameters = $"public_id={publicId}&timestamp={timestamp}";
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parameters + settings.ApiSecret))).ToLowerInvariant();
        using var body = new MultipartFormDataContent();
        using var apiKeyContent = new StringContent(settings.ApiKey);
        using var timestampContent = new StringContent(timestamp);
        using var publicIdContent = new StringContent(publicId);
        using var signatureContent = new StringContent(signature);
        body.Add(apiKeyContent, "api_key");
        body.Add(timestampContent, "timestamp");
        body.Add(publicIdContent, "public_id");
        body.Add(signatureContent, "signature");
        using var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        body.Add(file, "file", "product" + extension);
        using var response = await (http ?? SharedHttp).PostAsync(
            $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(settings.CloudName)}/image/upload", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ValidationException("Không thể tải ảnh lên Cloudinary. Vui lòng kiểm tra cấu hình hoặc thử lại.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("secure_url", out var url) || string.IsNullOrWhiteSpace(url.GetString()))
            throw new ValidationException("Cloudinary không trả về đường dẫn ảnh hợp lệ");
        return url.GetString()!;
    }
}
