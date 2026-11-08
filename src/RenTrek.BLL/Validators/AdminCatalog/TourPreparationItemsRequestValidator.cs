using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra danh sách đồ tự mang (FR-T04).</summary>
public sealed class TourPreparationItemsRequestValidator : AbstractValidator<TourPreparationItemsRequest>
{
    public TourPreparationItemsRequestValidator()
    {
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ItemName).Text("Tên đồ", 255);
            item.RuleFor(x => x.Note).MaximumLength(1000).WithMessage("Ghi chú tối đa 1000 ký tự");
        });
    }
}
