namespace RenTrek.BLL.Services.Interfaces;

/// <summary>
/// Storage riêng tư cho chứng từ hoàn tiền: không có URL public, chỉ đọc lại qua server sau khi kiểm tra quyền.
/// </summary>
public interface IPrivateFileStorage
{
    /// <summary>Lưu file, trả về khóa lưu trữ (storage path) do server sinh.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Mở file đã lưu để đọc; ném NotFoundException khi không còn file.</summary>
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Xóa file (bỏ qua lỗi / file không tồn tại).</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
