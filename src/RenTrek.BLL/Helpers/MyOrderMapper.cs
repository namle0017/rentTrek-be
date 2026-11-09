using RenTrek.BLL.Constants;
using RenTrek.BLL.DTOs.Order.Requests;
using RenTrek.BLL.DTOs.Order.Responses;
using RenTrek.DAL.Models;

namespace RenTrek.BLL.Helpers;

/// <summary>
/// Chuyển đổi request/kết quả truy vấn của danh sách đơn hàng của tôi (dựng bộ lọc và tiêu đề đơn).
/// </summary>
public static class MyOrderMapper
{
    private const string DefaultRentalTitle = "Thuê thiết bị";
    private const string DefaultTripTitle = "Chuyến đi";

    /// <summary>Chuyển request sang bộ lọc DAL (chuẩn hoá chuỗi, giữ paging đã được clamp bởi PagingRequest).</summary>
    public static MyOrderFilter ToFilter(MyOrderQueryRequest request)
    {
        return new MyOrderFilter
        {
            OrderType = NormalizeOrderType(request.OrderType),
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim(),
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            SortBy = string.Equals(request.SortBy, "serviceStartAt", StringComparison.OrdinalIgnoreCase)
                ? "serviceStartAt"
                : "createdAt",
            IsDescending = request.IsDescending,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    /// <summary>Chuyển một dòng DAL sang response, ghép tiêu đề.</summary>
    public static MyOrderResponse ToResponse(MyOrderRow row)
    {
        return new MyOrderResponse
        {
            Id = row.OrderId,
            OrderCode = row.OrderCode,
            OrderType = row.OrderType,
            Status = row.Status,
            PaymentStatus = row.PaymentStatus,
            TotalAmount = row.TotalAmount,
            TotalPayable = row.TotalAmount,
            ServiceStartAt = row.ServiceStartAt,
            ServiceEndAt = row.ServiceEndAt,
            Title = BuildTitle(row),
            HoldExpiresAt = row.HoldExpiresAt,
            CreatedAt = row.CreatedAt
        };
    }

    /// <summary>Đơn đặt chuyến: tên tour; đơn thuê: tên dòng đầu tiên kèm " +N" khi có nhiều dòng.</summary>
    public static string BuildTitle(MyOrderRow row)
    {
        if (string.Equals(row.OrderType, OrderTypes.TripBooking, StringComparison.Ordinal))
        {
            return row.TourName ?? DefaultTripTitle;
        }

        var name = row.FirstItemName ?? DefaultRentalTitle;
        return row.ItemCount > 1 ? $"{name} +{row.ItemCount - 1}" : name;
    }

    private static string? NormalizeOrderType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, OrderTypes.Rental, StringComparison.OrdinalIgnoreCase))
        {
            return OrderTypes.Rental;
        }

        return string.Equals(trimmed, OrderTypes.TripBooking, StringComparison.OrdinalIgnoreCase)
            ? OrderTypes.TripBooking
            : trimmed;
    }
}
