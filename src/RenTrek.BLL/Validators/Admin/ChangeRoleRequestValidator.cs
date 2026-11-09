using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role).OneOf("Vai trò", "Customer", "Staff", "Guide");
        RuleFor(x => x.Reason).Text("Lý do", 500);
    }
}
