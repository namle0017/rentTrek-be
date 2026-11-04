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
