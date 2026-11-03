using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.TripBooking.Requests;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Kiểm tra định dạng yêu cầu hủy. Tài khoản có bắt buộc hay không do Service quyết định theo số tiền hoàn.
/// </summary>
public class CancelTripBookingRequestValidator : AbstractValidator<CancelTripBookingRequest>
{
    public CancelTripBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Lý do không vượt quá 500 ký tự");

        When(x => x.BankAccount is not null, () =>
        {
            // Đặt tên field tường minh để khớp field lồng trong form FE
            RuleFor(x => x.BankAccount!.BankCode)
                .Must(RefundBankCodes.All.Contains).WithMessage("Ngân hàng không được hỗ trợ")
                .OverridePropertyName("bankAccount.bankCode");
            RuleFor(x => x.BankAccount!.AccountNumber)
                .Matches(@"^\d{6,20}$").WithMessage("Số tài khoản gồm 6–20 chữ số")
                .OverridePropertyName("bankAccount.accountNumber");
            RuleFor(x => x.BankAccount!.AccountHolder)
                .Must(holder => holder?.Trim().Length is >= 2 and <= 100).WithMessage("Tên chủ tài khoản gồm 2–100 ký tự")
                .OverridePropertyName("bankAccount.accountHolder");
        });
    }
}
