using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu cập nhật quy tắc lặp chuyến.
/// </summary>
public sealed class UpdateRecurrenceRuleRequestValidator : AbstractValidator<UpdateRecurrenceRuleRequest>
{
    private static readonly string[] AllowedFrequencies = ["Daily", "Weekly", "Monthly"];

    public UpdateRecurrenceRuleRequestValidator()
    {
        RuleFor(x => x.Frequency)
            .NotEmpty().WithMessage("Tần suất lặp là bắt buộc")
            .Must(f => AllowedFrequencies.Contains(f, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Tần suất lặp chỉ chấp nhận Daily, Weekly hoặc Monthly");

        RuleFor(x => x.IntervalValue)
            .GreaterThanOrEqualTo(1).WithMessage("Khoảng cách lặp phải lớn hơn hoặc bằng 1");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Giờ khởi hành là bắt buộc")
            .Must(t => TimeOnly.TryParse(t, out _))
            .WithMessage("Giờ khởi hành không đúng định dạng HH:mm:ss");

        RuleFor(x => x.DefaultTotalSlots)
            .GreaterThanOrEqualTo(1).WithMessage("Tổng số chỗ mặc định phải lớn hơn hoặc bằng 1");

        RuleFor(x => x.DefaultPrice)
            .GreaterThanOrEqualTo(0).When(x => x.DefaultPrice.HasValue)
            .WithMessage("Giá mặc định không được âm");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue)
            .WithMessage("Ngày kết thúc quy tắc lặp phải lớn hơn hoặc bằng ngày bắt đầu");
    }
}
