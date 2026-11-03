using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Warehouse.Requests;

namespace RenTrek.BLL.Validators.Warehouse;

public class BulkCreateAssetsRequestValidator : AbstractValidator<BulkCreateAssetsRequest>
{
    public BulkCreateAssetsRequestValidator()
    {
        RuleFor(x => x.ProductVariantId)
            .NotEmpty().WithMessage("Mã biến thể sản phẩm không được để trống.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 100).WithMessage("Số lượng thiết bị tạo hàng loạt phải từ 1 đến 100.");

        RuleFor(x => x.CodePrefix)
            .NotEmpty().WithMessage("Tiền tố mã thiết bị (prefix) không được để trống.")
            .MaximumLength(30).WithMessage("Tiền tố mã thiết bị tối đa 30 ký tự.");

        RuleFor(x => x.InitialConditionNote)
            .MaximumLength(500).WithMessage("Ghi chú ban đầu tối đa 500 ký tự.");
    }
}

public class UpdateAssetStatusRequestValidator : AbstractValidator<UpdateAssetStatusRequest>
{
    private static readonly string[] ValidStatuses =
    [
        EquipmentAssetStatuses.Available,
        EquipmentAssetStatuses.Maintenance,
        EquipmentAssetStatuses.Damaged,
        EquipmentAssetStatuses.Lost,
        EquipmentAssetStatuses.Retired
    ];

    public UpdateAssetStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .NotEmpty().WithMessage("Trạng thái mới không được để trống.")
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage($"Trạng thái phải là một trong các giá trị: {string.Join(", ", ValidStatuses)}.");

        RuleFor(x => x.ConditionNote)
            .NotEmpty().WithMessage("Ghi chú lý do cập nhật tình trạng không được để trống.")
            .MaximumLength(500).WithMessage("Ghi chú tình trạng tối đa 500 ký tự.");
    }
}

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
