using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body sắp lại ảnh: không trùng id, ảnh đại diện nằm trong danh sách.</summary>
public sealed class ReorderTourImagesRequestValidator : AbstractValidator<ReorderTourImagesRequest>
{
    public ReorderTourImagesRequestValidator()
    {
        RuleFor(x => x.ImageIdsInOrder).Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách ảnh bị trùng");
        RuleFor(x => x.PrimaryImageId).Must((x, id) => id == null || x.ImageIdsInOrder.Contains(id.Value))
            .WithMessage("Ảnh đại diện phải nằm trong danh sách ảnh");
    }
}
