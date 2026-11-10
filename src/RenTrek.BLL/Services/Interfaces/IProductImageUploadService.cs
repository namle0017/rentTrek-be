namespace RenTrek.BLL.Services.Interfaces;

public interface IProductImageUploadService
{
    const long MaxBytes = 5 * 1024 * 1024;
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default);
}
