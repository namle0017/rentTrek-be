using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using RenTrek.BLL.Exceptions;
using RenTrek.BLL.Options;
using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.BLL.Services.Implementations;

/// <summary>
/// Lưu chứng từ vào bucket Supabase PRIVATE (tải về phía server bằng service key); chưa cấu hình Supabase thì lưu vào
/// App_Data/private-uploads/refund-proofs dưới content root, ngoài wwwroot nên không truy cập trực tiếp được.
/// </summary>
public sealed class PrivateFileStorage(IOptions<SupabaseStorageSettings> supabase) : IPrivateFileStorage
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly SupabaseStorageSettings _supabase = supabase.Value;

    private static string LocalRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "private-uploads", "refund-proofs"));

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        // Tên do server sinh, không dùng tên gốc để tránh path traversal
        var path = $"{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}{ExtensionOf(contentType)}";
        if (_supabase.IsConfigured)
        {
            using var request = SupabaseRequest(HttpMethod.Post, path);
            request.Content = new StreamContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Supabase Storage trả {(int)response.StatusCode}");
            return path;
        }

        var fullPath = ResolveLocal(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(destination, cancellationToken);
        return path;
    }

    public async Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (_supabase.IsConfigured)
        {
            using var request = SupabaseRequest(HttpMethod.Get, storagePath);
            using var response = await Http.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                throw new NotFoundException("Không tìm thấy chứng từ");
            response.EnsureSuccessStatusCode();
            // File tối đa vài MB: đọc hết vào bộ nhớ để không phụ thuộc vòng đời response
            var buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return buffer;
        }

        var fullPath = ResolveLocal(storagePath);
        if (!File.Exists(fullPath))
            throw new NotFoundException("Không tìm thấy chứng từ");
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return;
        try
        {
            if (_supabase.IsConfigured)
            {
                using var request = SupabaseRequest(HttpMethod.Delete, storagePath);
                using var _ = await Http.SendAsync(request, cancellationToken);
                return;
            }

            var fullPath = ResolveLocal(storagePath);
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch
        {
            // Dọn file lỗi không được che lỗi gốc
        }
    }

    private static string ResolveLocal(string storagePath)
    {
        var root = LocalRoot + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(LocalRoot, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
            throw new NotFoundException("Không tìm thấy chứng từ");
        return fullPath;
    }

    private HttpRequestMessage SupabaseRequest(HttpMethod method, string objectPath)
    {
        var request = new HttpRequestMessage(
            method, $"{_supabase.Url.TrimEnd('/')}/storage/v1/object/{_supabase.PrivateBucket}/{objectPath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabase.ServiceKey);
        request.Headers.Add("apikey", _supabase.ServiceKey);
        return request;
    }

    private static string ExtensionOf(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };
}
