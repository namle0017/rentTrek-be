using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Kiểm tra body thêm ảnh tour / biến thể: URL tuyệt đối http(s) (FR-T02, FR-P01).</summary>
public sealed class SaveImageRequestValidator : AbstractValidator<SaveImageRequest>
{
    public SaveImageRequestValidator()
    {
        RuleFor(x => x.ImageUrl).Text("URL ảnh", 2000)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("URL ảnh phải là địa chỉ http(s) hợp lệ");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage("Thứ tự hiển thị không được âm");
    }
}
