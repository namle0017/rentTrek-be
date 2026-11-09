namespace RenTrek.BLL.Constants;

/// <summary>
/// Whitelist mã ngân hàng nhận hoàn tiền; FE giữ danh sách tương ứng trong constants/enums.js.
/// </summary>
public static class RefundBankCodes
{
    /// <summary>Phiên bản nội dung đồng ý xử lý dữ liệu ngân hàng (§10).</summary>
    public const string ConsentVersion = "refund-bank-data-v1";

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["VCB"] = "Vietcombank",
        ["BIDV"] = "BIDV",
        ["ICB"] = "VietinBank",
        ["VBA"] = "Agribank",
        ["TCB"] = "Techcombank",
        ["MB"] = "MB Bank",
        ["ACB"] = "ACB",
        ["VPB"] = "VPBank",
        ["TPB"] = "TPBank",
        ["STB"] = "Sacombank",
        ["HDB"] = "HDBank",
        ["VIB"] = "VIB",
        ["SHB"] = "SHB",
        ["MSB"] = "MSB",
        ["OCB"] = "OCB",
        ["SSB"] = "SeABank"
    };

    public static readonly IReadOnlySet<string> All = new HashSet<string>(Names.Keys, StringComparer.Ordinal);
}
