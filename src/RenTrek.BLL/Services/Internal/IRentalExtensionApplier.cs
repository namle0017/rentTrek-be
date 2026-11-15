using RenTrek.DAL.Entities;

namespace RenTrek.BLL.Services.Internal;

/// <summary>Áp gia hạn đơn thuê đã thanh toán (gọi từ webhook, trong transaction của caller, không SaveChanges).</summary>
public interface IRentalExtensionApplier
{
    Task ApplyPaidExtensionAsync(Order order, RentalOrderAdjustment adjustment, DateTime nowUtc, CancellationToken cancellationToken = default);
}
