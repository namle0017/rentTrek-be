namespace RenTrek.BLL.Services.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAvatarAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileLength,
        Guid userId,
        CancellationToken cancellationToken = default);

    void DeleteFile(string? relativePath);
}
