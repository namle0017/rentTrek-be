using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body sắp lại hoạt động trong ngày.</summary>
public sealed class ReorderTourActivitiesRequestValidator : AbstractValidator<ReorderTourActivitiesRequest>
{
    public ReorderTourActivitiesRequestValidator()
    {
        RuleFor(x => x.DayNumber).GreaterThanOrEqualTo(1).WithMessage("Ngày phải từ 1 trở lên");
        RuleFor(x => x.ActivityIdsInOrder).Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách hoạt động bị trùng");
    }
}
