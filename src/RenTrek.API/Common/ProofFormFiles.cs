using RenTrek.BLL.DTOs.Refund.Internal;

namespace RenTrek.API.Common;

/// <summary>
/// Chuyển file multipart của chứng từ hoàn tiền sang đầu vào cho service và dọn stream sau khi dùng.
/// </summary>
public static class ProofFormFiles
{
    public static List<ProofFileInput> ToInputs(IEnumerable<IFormFile>? files)
        => (files ?? [])
            .Select(file => new ProofFileInput(file.OpenReadStream(), file.FileName, file.ContentType, file.Length))
            .ToList();

    public static void DisposeAll(IEnumerable<ProofFileInput> inputs)
    {
        foreach (var input in inputs)
            input.Content.Dispose();
    }
}
