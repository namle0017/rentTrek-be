using FluentValidation;
using RenTrek.BLL.DTOs.Admin.Requests;

namespace RenTrek.BLL.Validators.Admin;

public sealed class ParticipantOverrideRequestValidator : AbstractValidator<ParticipantOverrideRequest>
{
    public ParticipantOverrideRequestValidator()
    {
        RuleFor(x => x.FullName).Text("Họ và tên", 100);
        RuleFor(x => x.Phone).Phone();
        RuleFor(x => x.EmergencyContactName).MaximumLength(200).WithMessage("Tên người liên hệ tối đa 200 ký tự");
        RuleFor(x => x.EmergencyContactPhone).Phone();
        RuleFor(x => x.DateOfBirth).NotFuture("Ngày sinh không hợp lệ");
        RuleFor(x => x.Reason).Text("Lý do", 500);
    }
}
