using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Internal;

/// <summary>Số tiền đơn phải trả khi thanh toán lần đầu.</summary>
public static class OrderPayableAmount
{
    /// <summary>Đơn thuê: tiền thuê (sau giảm giá) + cọc. Đơn tour: tổng tiền.</summary>
    public static decimal Of(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.TotalAmount + (order.RentalOrder?.DepositAmount ?? 0m);
    }
}
