using FluentValidation;
using RenTrek.BLL.DTOs.RentalOrder.Requests;

namespace RenTrek.BLL.Validators.RentalOrder;

/// <summary>
/// Kiểm tra định dạng yêu cầu báo giá gia hạn: bắt buộc có thời điểm trả mới. Điều kiện nghiệp vụ được kiểm ở service để trả đúng mã lỗi.
/// </summary>
public class ExtendRentalQuoteRequestValidator : AbstractValidator<ExtendRentalQuoteRequest>
{
    public ExtendRentalQuoteRequestValidator()
    {
        RuleFor(request => request.NewEndAt)
            .NotEqual(default(DateTime)).WithMessage("Thời điểm trả mới là bắt buộc");
    }
}
