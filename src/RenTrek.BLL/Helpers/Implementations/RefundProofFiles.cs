using RenTrek.BLL.DTOs.Refund.Internal;
using RenTrek.BLL.Exceptions;

namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>
/// Quy tắc ảnh chứng từ hoàn tiền: 1..3 ảnh, JPG/PNG/WebP, tối đa 5MB mỗi ảnh, kiểm tra bằng magic bytes.
/// </summary>
public static class RefundProofFiles
{
    public const int MaxFiles = 3;
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string Field = "files";

    /// <summary>Xác định loại ảnh theo byte đầu file; null nếu không phải JPG/PNG/WebP.</summary>
    public static string? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    /// <summary>Lỗi của một ảnh (null nếu hợp lệ). <paramref name="label"/> dùng để người dùng biết ảnh nào lỗi.</summary>
    public static string? Validate(byte[] data, string label)
    {
        if (data.Length == 0)
            return $"{label}: file rỗng";
        if (data.Length > MaxBytes)
            return $"{label}: dung lượng tối đa {MaxBytes / 1024 / 1024}MB";
        return Sniff(data) is null ? $"{label}: chỉ nhận ảnh JPG, PNG hoặc WebP" : null;
    }

    /// <summary>Đọc và kiểm tra danh sách file tải lên; ném ValidationException (key "files") khi sai.</summary>
    public static async Task<List<RefundProofFile>> ReadAsync(
        IReadOnlyList<ProofFileInput>? files,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
            throw new ValidationException(Field, "Cần đính kèm 1 đến 3 ảnh chứng từ chuyển khoản");
        if (files.Count > MaxFiles)
            throw new ValidationException(Field, $"Tối đa {MaxFiles} ảnh chứng từ");

        var result = new List<RefundProofFile>(files.Count);
        foreach (var file in files)
        {
            var name = SafeName(file.FileName, null);
            if (file.Length > MaxBytes)
                throw new ValidationException(Field, $"{name}: dung lượng tối đa {MaxBytes / 1024 / 1024}MB");

            // Đọc tối đa MaxBytes + 1 byte: không tin Length do client khai
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await file.Content.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                    break;
            }

            var data = buffer.ToArray();
            var error = Validate(data, name);
            if (error is not null)
                throw new ValidationException(Field, error);
            var contentType = Sniff(data)!;
            result.Add(new RefundProofFile(data, SafeName(file.FileName, contentType), contentType));
        }

        return result;
    }

    /// <summary>Tên hiển thị an toàn (bỏ đường dẫn, tối đa 200 ký tự).</summary>
    public static string SafeName(string? fileName, string? contentType)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/')).Trim();
        if (name.Length > 200)
            name = name[^200..];
        if (name.Length > 0)
            return name;
        return contentType switch
        {
            "image/png" => "proof.png",
            "image/webp" => "proof.webp",
            _ => "proof.jpg"
        };
    }
}
