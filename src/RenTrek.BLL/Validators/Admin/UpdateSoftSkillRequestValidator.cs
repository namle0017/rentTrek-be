using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class UpdateSoftSkillRequestValidator : AbstractValidator<UpdateSoftSkillRequest>
{
    public UpdateSoftSkillRequestValidator() => Include(new CreateSoftSkillRequestValidator());
}
