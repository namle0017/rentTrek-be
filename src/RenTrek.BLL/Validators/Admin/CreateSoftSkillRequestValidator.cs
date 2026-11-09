using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class CreateSoftSkillRequestValidator : AbstractValidator<CreateSoftSkillRequest>
{
    public CreateSoftSkillRequestValidator() => RuleFor(x => x.Name).Text("Tên kỹ năng", 100);
}
