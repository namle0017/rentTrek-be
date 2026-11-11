using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

/// <summary>
/// Kiểm tra định dạng yêu cầu đánh dấu hoàn tiền thủ công.
/// </summary>
public sealed class MarkManualRefundRequestValidator : AbstractValidator<MarkManualRefundRequest>
{
    public MarkManualRefundRequestValidator()
    {
        RuleFor(x => x.ManualReference).Text("Mã tham chiếu", 100);
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự");
    }
}
