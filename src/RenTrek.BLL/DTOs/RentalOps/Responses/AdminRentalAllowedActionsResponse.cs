namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalAllowedActionsResponse
{
    public bool CanHandover { get; set; }
    public string? HandoverBlockedReason { get; set; }
    public bool CanReturn { get; set; }
    public bool CanAddCharge { get; set; }
    public bool CanComplete { get; set; }
    public string? CompleteBlockedReason { get; set; }
    /// <summary>Đã tới mốc khách không đến nhận (staff được báo no-show).</summary>
    public bool CanReportNoShow { get; set; }
    /// <summary>Đã nhận lại đủ thiết bị, chưa có báo cáo đang chờ duyệt.</summary>
    public bool CanSubmitReturnReport { get; set; }
    public int MinPhotosPerAsset { get; set; }
}
