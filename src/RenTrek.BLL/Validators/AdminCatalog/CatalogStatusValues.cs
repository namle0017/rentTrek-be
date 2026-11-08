using FluentValidation;
using RenTrek.BLL.DTOs.AdminCatalog.Requests;
using RenTrek.BLL.Validators.Admin;

namespace RenTrek.BLL.Validators.AdminCatalog;

/// <summary>Giá trị trạng thái admin được gửi cho sản phẩm / biến thể / combo.</summary>
internal static class CatalogStatusValues
{
    public static readonly string[] All = ["Active", "Inactive", "Draft"];
}
