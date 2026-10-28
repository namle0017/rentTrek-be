using FluentValidation;
using RenTrek.BLL.DTOs.RentalOrder.Requests;

namespace RenTrek.BLL.Validators.RentalOrder;

/// <summary>
/// Kiểm tra dữ liệu và quy tắc hợp lệ cho create rental order request.
/// </summary>
public class CreateRentalOrderRequestValidator : AbstractValidator<CreateRentalOrderRequest>
{
    public const int MaxNoteLength = 1000;
    public const int MaxReceiverNameLength = 255;
    public const string PickupMethodStore = "Store";

    // SĐT di động Việt Nam: 0xxxxxxxxx hoặc +84/84xxxxxxxxx (đầu số 3, 5, 7, 8, 9)
    private const string VietnamPhonePattern = @"^(0|\+?84)(3|5|7|8|9)\d{8}$";

    public CreateRentalOrderRequestValidator()
    {
        Include(new RentalQuoteRequestValidator());

        RuleFor(request => request.Note)
            .MaximumLength(MaxNoteLength)
            .WithMessage($"Ghi chú tối đa {MaxNoteLength} ký tự");

        RuleFor(request => request.AcceptedRefundPolicyVersion)
            .GreaterThan(0)
            .WithMessage("Phiên bản chính sách hoàn tiền đã đồng ý là bắt buộc");

        When(request => request.Pickup is not null, () =>
        {
            RuleFor(request => request.Pickup!.ReceiverName)
                .MaximumLength(MaxReceiverNameLength)
                .WithMessage($"Tên người nhận tối đa {MaxReceiverNameLength} ký tự");

            RuleFor(request => request.Pickup!.ReceiverPhone)
                .Matches(VietnamPhonePattern)
                .When(request => !string.IsNullOrWhiteSpace(request.Pickup!.ReceiverPhone))
                .WithMessage("Số điện thoại người nhận không hợp lệ");

            RuleFor(request => request.Pickup!.Method)
                .Must(method => string.Equals(method?.Trim(), PickupMethodStore, StringComparison.OrdinalIgnoreCase))
                .When(request => !string.IsNullOrWhiteSpace(request.Pickup!.Method))
                .WithMessage($"Hình thức nhận đồ chỉ hỗ trợ {PickupMethodStore}");
        });
    }
}
