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
}
