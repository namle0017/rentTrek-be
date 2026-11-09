using FluentValidation;
using RenTrek.BLL.DTOs.Warehouse.Requests;

namespace RenTrek.BLL.Validators.Warehouse;

public class AssetGanttQueryRequestValidator : AbstractValidator<AssetGanttQueryRequest>
{
    public AssetGanttQueryRequestValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");

        RuleFor(x => (x.To - x.From).TotalDays)
            .LessThanOrEqualTo(62)
            .WithMessage("Khoảng thời gian xem biểu đồ Gantt tối đa là 62 ngày (2 tháng).");
    }
}
