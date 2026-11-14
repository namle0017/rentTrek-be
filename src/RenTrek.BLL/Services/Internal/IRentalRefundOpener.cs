using RenTrek.BLL.DTOs.Refund.Requests;
using RenTrek.BLL.DTOs.Refund.Responses;
using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Internal;

/// <summary>Mở hồ sơ hoàn tiền cho đơn thuê trong transaction của caller (huỷ, no-show, hoàn cọc).</summary>
public interface IRentalRefundOpener
{
    /// <summary>Không có STK thì hồ sơ ở AwaitingBankInfo chờ khách nhập; không tự commit.</summary>
    Task<RefundCaseSummaryResponse> OpenRentalRefundAsync(
        Order order,
        decimal amount,
        string source,
        RefundBankAccountRequest? bankAccount,
        string? consentVersion,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
