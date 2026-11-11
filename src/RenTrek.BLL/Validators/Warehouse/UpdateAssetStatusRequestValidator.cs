using FluentValidation;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Warehouse.Requests;

namespace RenTrek.BLL.Validators.Warehouse;

public class UpdateAssetStatusRequestValidator : AbstractValidator<UpdateAssetStatusRequest>
{
    private static readonly string[] ValidStatuses =
    [
        EquipmentAssetStatuses.Available,
        EquipmentAssetStatuses.Good,
        EquipmentAssetStatuses.Maintenance,
        EquipmentAssetStatuses.Damaged,
        EquipmentAssetStatuses.Lost,
        EquipmentAssetStatuses.Retired
    ];

    public UpdateAssetStatusRequestValidator()
    {
        RuleFor(x => !string.IsNullOrWhiteSpace(x.NewStatus) ? x.NewStatus : x.AssetStatus)
            .NotEmpty().WithMessage("Trạng thái mới không được để trống.")
            .Must(s => string.IsNullOrEmpty(s) || ValidStatuses.Contains(s))
            .WithMessage($"Trạng thái phải là một trong các giá trị: {string.Join(", ", ValidStatuses)}.");

        RuleFor(x => x.ConditionNote)
            .MaximumLength(500).WithMessage("Ghi chú tình trạng tối đa 500 ký tự.");
    }
}
