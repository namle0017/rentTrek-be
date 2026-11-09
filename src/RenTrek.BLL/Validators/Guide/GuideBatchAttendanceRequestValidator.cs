using RenTrek.BLL.Constants;
using FluentValidation;
using RenTrek.BLL.DTOs.Guide.Requests;

namespace RenTrek.BLL.Validators.Guide;

/// <summary>
/// Kiểm tra tính hợp lệ của yêu cầu điểm danh hàng loạt của Hướng dẫn viên.
/// </summary>
public sealed class GuideBatchAttendanceRequestValidator : AbstractValidator<GuideBatchAttendanceRequest>
{
    private static readonly string[] AllowedStatuses = [AttendanceStatuses.Present, AttendanceStatuses.Absent, AttendanceStatuses.Late, AttendanceStatuses.Excused];

    public GuideBatchAttendanceRequestValidator()
    {
        RuleFor(x => x.ActivityId)
            .NotEmpty().WithMessage("Mã hoạt động là bắt buộc");

        RuleFor(x => x)
            .Must(x => x.MarkAllPresent || (x.Attendances is not null && x.Attendances.Count > 0))
            .WithMessage("Phải chọn Tất cả có mặt hoặc gửi ít nhất 1 bản ghi điểm danh");

        RuleForEach(x => x.Attendances).ChildRules(record =>
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
