using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên thẻ", 100);
        RuleFor(x => x.Slug).Text("Slug", 200)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$").WithMessage("Slug chỉ gồm chữ thường, số và dấu gạch ngang");
    }
}
