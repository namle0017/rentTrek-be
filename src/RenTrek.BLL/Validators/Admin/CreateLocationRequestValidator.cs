using System.Text.RegularExpressions;
using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class CreateLocationRequestValidator : AbstractValidator<CreateLocationRequest>
{
    public CreateLocationRequestValidator()
    {
        RuleFor(x => x.Name).Text("Tên địa điểm", 200);
        RuleFor(x => x.Address).Text("Địa chỉ", 500);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).WithMessage("Vĩ độ phải từ -90 đến 90");
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).WithMessage("Kinh độ phải từ -180 đến 180");
    }
}
