namespace RenTrek.BLL.DTOs.RentalOps.Requests;

/// <summary>Quét / nhập mã một thiết bị để giao (FR-R06).</summary>
public sealed class ScanHandoverRequest
{
    public string AssetCode { get; set; } = string.Empty;
}
