using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body hoạt động lịch trình; giới hạn trên của ngày kiểm trong service theo số ngày tour (FR-T03).</summary>
public sealed class SaveTourActivityRequestValidator : AbstractValidator<SaveTourActivityRequest>
{
    public SaveTourActivityRequestValidator()
    {
        RuleFor(x => x.DayNumber).GreaterThanOrEqualTo(1).WithMessage("Ngày phải từ 1 trở lên");
        RuleFor(x => x.DayTitle).MaximumLength(255).WithMessage("Tiêu đề ngày tối đa 255 ký tự");
        RuleFor(x => x.StartTime).Must(t => t >= TimeSpan.Zero && t < TimeSpan.FromDays(1)).WithMessage("Giờ bắt đầu phải trong ngày (00:00–23:59)");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).WithMessage("Thời lượng phải lớn hơn 0");
        RuleFor(x => x.Name).Text("Tên hoạt động", 255);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).When(x => x.DisplayOrder.HasValue).WithMessage("Thứ tự không được âm");
    }
}
