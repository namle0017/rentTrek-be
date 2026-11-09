using FluentValidation;
using RenTrek.BLL.DTOs.RentalOrder.Requests;

namespace RenTrek.BLL.Validators.RentalOrder;

/// <summary>
/// Kiểm tra định dạng yêu cầu áp dụng gia hạn: bắt buộc có thời điểm trả mới và số tiền thêm đã đồng ý không âm.
/// </summary>
public class ExtendRentalOrderRequestValidator : AbstractValidator<ExtendRentalOrderRequest>
{
    public ExtendRentalOrderRequestValidator()
    {
        RuleFor(request => request.NewEndAt)
            .NotEqual(default(DateTime)).WithMessage("Thời điểm trả mới là bắt buộc");

        RuleFor(request => request.AcceptedExtraAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền gia hạn đã đồng ý không được âm");
    }
}
