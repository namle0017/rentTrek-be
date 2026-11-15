using FluentValidation;
using RenTrek.BLL.DTOs.TripBooking.Requests;

namespace RenTrek.BLL.Validators.Refund;

public class CancelTripParticipantsRequestValidator : AbstractValidator<CancelTripParticipantsRequest>
{
    public CancelTripParticipantsRequestValidator()
    {
        Include(new CancelTripBookingRequestValidator());
        RuleFor(x => x.ParticipantIds)
            .NotEmpty().WithMessage("Chọn ít nhất 1 hành khách cần hủy")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách hành khách bị trùng");
    }
}
