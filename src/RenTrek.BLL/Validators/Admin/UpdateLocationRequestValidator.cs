using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator() => Include(new CreateLocationRequestValidator());
}
