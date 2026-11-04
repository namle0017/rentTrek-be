using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class StatusRequestValidator : AbstractValidator<StatusRequest>
{
    // Hiện chỉ dùng cho trạng thái khuyến mãi.
    public StatusRequestValidator() => RuleFor(x => x.Status).OneOf("Trạng thái", "Active", "Paused", "Expired");
}
