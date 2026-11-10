namespace RenTrek.BLL.DTOs.Admin.Responses;

/// <summary>
/// Thẻ KPI màn O27. Số tiền dương; NetInflow = TotalCollectionSucceeded - TotalRefundSucceeded.
/// </summary>
public sealed record AdminPaymentStatsResponse(
    decimal TotalCollectionSucceeded,
    decimal TotalRefundSucceeded,
    decimal NetInflow,
    int PendingCount,
    decimal PendingAmount);
