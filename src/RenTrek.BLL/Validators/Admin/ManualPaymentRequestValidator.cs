using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class ManualPaymentRequestValidator : AbstractValidator<ManualPaymentRequest>
{
    public ManualPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).Must(x => x > 0 && x == decimal.Truncate(x)).WithMessage("Số tiền VND phải là số nguyên dương");
        RuleFor(x => x.PaymentMethod).OneOf("Phương thức thanh toán", "BankTransfer", "Cash");
        RuleFor(x => x.ManualReference).Text("Mã tham chiếu", 100);
        RuleFor(x => x.Reason).Text("Lý do", 500);
    }
}
