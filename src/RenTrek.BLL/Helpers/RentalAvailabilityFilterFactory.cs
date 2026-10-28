using System.Globalization;
using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Rental;
using RenTrek.DAL.Models;
using RenTrek.DAL.UnitOfWorks;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Dựng bộ lọc tính tài sản khả dụng cho một khoảng thuê: cửa sổ [start − đệm, end + đệm), trạng thái cho thuê,
/// trạng thái phân bổ chặn (Reserved, Issued). Đệm vệ sinh đọc từ cấu hình rental.cleaningBufferHours.
/// </summary>
public static class RentalAvailabilityFilterFactory
{
    private static readonly string[] BlockingStatuses = [AssignmentStatuses.Reserved, AssignmentStatuses.Issued];

    /// <summary>Số giờ đệm vệ sinh (không âm); thiếu hoặc sai định dạng cấu hình thì dùng giá trị mặc định.</summary>
    public static async Task<decimal> GetCleaningBufferHoursAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var raw = await unitOfWork.SystemConfigurations.GetValueAsync(RentalConfigKeys.CleaningBufferHours, cancellationToken);
        var hours = decimal.TryParse(raw?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : RentalConfigKeys.DefaultCleaningBufferHours;
        return Math.Max(0, hours);
    }

    /// <summary>Tạo bộ lọc khả dụng cho khoảng thuê (UTC) tại thời điểm <paramref name="nowUtc"/>.</summary>
    public static async Task<AssetAvailabilityFilter> CreateAsync(
        IUnitOfWork unitOfWork,
        RentalPeriod period,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var bufferHours = (double)await GetCleaningBufferHoursAsync(unitOfWork, cancellationToken);
        return new AssetAvailabilityFilter
        {
            WindowStart = period.StartUtc.AddHours(-bufferHours),
            WindowEnd = period.EndUtc.AddHours(bufferHours),
            NowUtc = nowUtc,
            RentableAssetStatuses = EquipmentAssetStatuses.Rentable,
            BlockingAssignmentStatuses = BlockingStatuses,
            IssuedStatus = AssignmentStatuses.Issued
        };
    }
}
