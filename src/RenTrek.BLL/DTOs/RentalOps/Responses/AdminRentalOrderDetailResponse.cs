namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalOrderDetailResponse
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public string? Note { get; set; }
    public string? HandledByName { get; set; }

    /// <summary>"Bây giờ" của server (theo đồng hồ giả lập nếu đang bật) để FE tính đếm ngược.</summary>
    public DateTime ServerNow { get; set; }

    public AdminRentalCustomerResponse Customer { get; set; } = new();
    public List<AdminRentalLineResponse> Lines { get; set; } = [];
    public List<AdminRentalAssignmentResponse> Assignments { get; set; } = [];
    public List<AdminRentalChargeResponse> Charges { get; set; } = [];
    public AdminRentalAmountsResponse Amounts { get; set; } = new();
    public AdminRentalOverdueResponse Overdue { get; set; } = new();
    public AdminRentalSettlementResponse Settlement { get; set; } = new();
    public AdminRentalAllowedActionsResponse AllowedActions { get; set; } = new();
    public RenTrek.BLL.DTOs.RentalPolicy.Responses.RentalPolicyResponse? RentalPolicy { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? CustomerReceivedConfirmedAt { get; set; }
    public string? ReceiptConfirmSource { get; set; }
    public string? CancellationType { get; set; }
    public List<RenTrek.BLL.DTOs.RentalOrder.Responses.RentalEvidencePhotoResponse> EvidencePhotos { get; set; } = [];
    public List<RenTrek.BLL.DTOs.RentalReport.Responses.RentalReportResponse> Reports { get; set; } = [];
    /// <summary>Quyết toán đã chốt (null nếu chưa).</summary>
    public RenTrek.BLL.DTOs.RentalOrder.Responses.RentalOrderSettlementResponse? StoredSettlement { get; set; }
}
