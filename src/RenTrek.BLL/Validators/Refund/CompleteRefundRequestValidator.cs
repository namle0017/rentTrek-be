using FluentValidation;
using RenTrek.BLL.DTOs.Refund.Requests;

namespace RenTrek.BLL.Validators.Refund;

/// <summary>
/// Kiểm tra thông tin giao dịch Admin khai báo.
/// </summary>
public class CompleteRefundRequestValidator : AbstractValidator<CompleteRefundRequest>
{
    public CompleteRefundRequestValidator()
    {
        RuleFor(x => x.ProviderTransactionId)
            .NotEmpty().WithMessage("Cần nhập mã giao dịch ngân hàng")
            .MaximumLength(255).WithMessage("Mã giao dịch không vượt quá 255 ký tự");
    }
}
