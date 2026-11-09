namespace RenTrek.BLL.Services.Interfaces;

public interface IFileStorageService
{
    /// <summary>Kiểm tra và lưu ảnh vào thư mục <paramref name="folder"/>, trả về URL hiển thị.</summary>
    Task<string> SaveImageAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength,
        string folder,
        long maxBytes,
        CancellationToken cancellationToken = default);

    /// <summary>Xóa ảnh do service này lưu; URL lạ (ảnh ngoài) thì bỏ qua.</summary>
    Task DeleteImageAsync(string? url, CancellationToken cancellationToken = default);
}
