namespace RenTrek.BLL.DTOs.RentalOps.Responses;

public sealed class AdminRentalOrderListItemResponse
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime RentalStartAt { get; set; }
    public DateTime RentalEndAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int AssetsCount { get; set; }
    public int IssuedCount { get; set; }
    public int ReturnedCount { get; set; }
    public int DraftChargesCount { get; set; }
    public bool IsOverdue { get; set; }
}

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

public sealed class AdminRentalCustomerResponse
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public sealed class AdminRentalLineResponse
{
    public Guid RentalOrderItemId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsBundle { get; set; }
    public int Quantity { get; set; }
    public decimal DailyPrice { get; set; }
    public int BillableDays { get; set; }
    public decimal LineAmount { get; set; }
}

public sealed class AdminRentalAssignmentResponse
{
    public Guid AssignmentId { get; set; }
    public Guid RentalOrderItemId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssetStatus { get; set; } = string.Empty;

    /// <summary>Thời điểm giao thực tế khi Status = Issued / Returned / LostOrException.</summary>
    public DateTime? IssuedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }
    public string? ReturnCondition { get; set; }
}

public sealed class AdminRentalChargeResponse
{
    public Guid ChargeId { get; set; }
    public string ChargeType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AssetCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
}

public sealed class AdminRentalAmountsResponse
{
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundedAmount { get; set; }
}

/// <summary>Tình trạng trễ hạn của thiết bị còn ở khách, tính theo "bây giờ" của server.</summary>
public sealed class AdminRentalOverdueResponse
{
    public bool IsOverdue { get; set; }
    public int OverdueHours { get; set; }
    public int LateDays { get; set; }
    public decimal GraceHours { get; set; }

    /// <summary>Phí trễ hệ thống sẽ đề xuất nếu nhận trả toàn bộ thiết bị còn lại ngay bây giờ.</summary>
    public decimal SuggestedLateFee { get; set; }
}

/// <summary>Quyết toán cọc (PRD §8.7). Chỉ là số tính toán, chưa chuyển tiền.</summary>
public sealed class AdminRentalSettlementResponse
{
    public decimal DepositHeld { get; set; }
    public decimal ValidCharges { get; set; }
    public decimal DepositToRefund { get; set; }
    public decimal AdditionalAmountDue { get; set; }
}

public sealed class AdminRentalAllowedActionsResponse
{
    public bool CanHandover { get; set; }
    public string? HandoverBlockedReason { get; set; }
    public bool CanReturn { get; set; }
    public bool CanAddCharge { get; set; }
    public bool CanComplete { get; set; }
    public string? CompleteBlockedReason { get; set; }
}
