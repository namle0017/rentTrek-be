using RenTrek.BLL.Constants;
using FluentValidation;
using RenTrek.BLL.DTOs.AdminTrip.Requests;

namespace RenTrek.BLL.Validators.AdminTrip;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu cập nhật điểm danh hoạt động.
/// </summary>
public sealed class UpdateTripAttendancesRequestValidator : AbstractValidator<UpdateTripAttendancesRequest>
{
    private static readonly string[] AllowedStatuses = [AttendanceStatuses.Present, AttendanceStatuses.Absent, AttendanceStatuses.Late, AttendanceStatuses.Excused];

    public UpdateTripAttendancesRequestValidator()
    {
        RuleFor(x => x.ActivityId)
            .NotEmpty().WithMessage("Mã hoạt động là bắt buộc");

        RuleFor(x => x.Records)
            .NotEmpty().WithMessage("Danh sách bản ghi điểm danh không được rỗng");

        RuleForEach(x => x.Records).ChildRules(record =>
        {
            record.RuleFor(r => r.ParticipantId)
                .NotEmpty().WithMessage("Mã hành khách là bắt buộc");

            record.RuleFor(r => r.Status)
                .NotEmpty().WithMessage("Trạng thái điểm danh là bắt buộc")
                .Must(s => AllowedStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
                .WithMessage("Trạng thái điểm danh chỉ chấp nhận Present, Absent, Late hoặc Excused");
        });
    }
}
