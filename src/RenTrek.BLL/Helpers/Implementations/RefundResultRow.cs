namespace RenTrek.BLL.Helpers.Implementations;

/// <summary>Một dòng dữ liệu kế toán điền trong file kết quả chuyển khoản.</summary>
public sealed class RefundResultRow
{
    public int RowNumber { get; set; }

    /// <summary>GUID đầy đủ từ cột ẩn RefundCaseId; null nếu thiếu / sai định dạng.</summary>
    public Guid? RefundCaseId { get; set; }

    /// <summary>Mã hồ sơ hiển thị (HT-xxxxxxxx).</summary>
    public string RefundCode { get; set; } = string.Empty;

    public string TransferredRaw { get; set; } = string.Empty;

    /// <summary>true = Có, false = Không, null = trống / không nhận ra.</summary>
    public bool? Transferred { get; set; }

    public string TransactionId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<(byte[] Data, string ContentType, string FileName)> Images { get; set; } = [];
}
