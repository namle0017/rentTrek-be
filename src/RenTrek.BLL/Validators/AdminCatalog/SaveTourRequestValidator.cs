using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body tạo / sửa tour: giá và số ngày đêm không âm (FR-T01).</summary>
public sealed class SaveTourRequestValidator : AbstractValidator<SaveTourRequest>
{
    public SaveTourRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên tour", 255);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Danh mục không được để trống");
        RuleFor(x => x.MainLocationId).NotEmpty().WithMessage("Địa điểm chính không được để trống");
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 365).WithMessage("Số ngày từ 1 đến 365");
        RuleFor(x => x.DurationNights).InclusiveBetween(0, 365).WithMessage("Số đêm từ 0 đến 365");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).WithMessage("Tổng thời lượng (phút) phải lớn hơn 0");
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0).WithMessage("Giá cơ sở không được âm");
    }
}
