using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class CreatePromotionRequestValidator : AbstractValidator<CreatePromotionRequest>
{
    public CreatePromotionRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên khuyến mãi", 200);
        RuleFor(x => x.PromotionCode).MaximumLength(50).WithMessage("Mã khuyến mãi tối đa 50 ký tự");
        RuleFor(x => x.StartAt).Must(x => x.Kind == DateTimeKind.Utc).WithMessage("Ngày bắt đầu phải dùng giờ UTC");
        RuleFor(x => x.EndAt)
            .Must(x => x.Kind == DateTimeKind.Utc).WithMessage("Ngày kết thúc phải dùng giờ UTC")
            .GreaterThan(x => x.StartAt).WithMessage("Ngày kết thúc phải sau ngày bắt đầu");
        RuleFor(x => x.AppliesTo).OneOf("Phạm vi áp dụng", "Tour", "Rental", "All");
        RuleFor(x => x.ApplicationScope).OneOf("Cấp áp dụng", "Order", "Line");
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses.HasValue).WithMessage("Giới hạn lượt dùng phải lớn hơn 0");
        RuleFor(x => x.MaxUsesPerUser).GreaterThan(0).When(x => x.MaxUsesPerUser.HasValue).WithMessage("Giới hạn mỗi người phải lớn hơn 0");
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0).WithMessage("Độ ưu tiên không được âm");
    }
}
