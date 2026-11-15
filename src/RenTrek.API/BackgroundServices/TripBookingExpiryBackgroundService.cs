using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.BackgroundServices;

/// <summary>
/// Mỗi 5 phút đóng các đơn chuyến đi quá hạn giữ chỗ mà chưa thanh toán (PendingPayment sang Expired).
/// Mỗi đơn chạy trong scope (DbContext) riêng để lỗi một đơn không lan sang đơn khác.
/// Dùng TimeProvider của DI nên đồng hồ giả lập ở Development cũng áp dụng.
/// </summary>
public sealed class TripBookingExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TripBookingExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Guid> orderIds;
        try
        {
            using var scope = scopeFactory.CreateScope();
            orderIds = await scope.ServiceProvider.GetRequiredService<ITripBookingExpiryService>().CollectExpiredAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Job hết hạn giữ chỗ: không gom được đơn quá hạn");
            return;
        }

        foreach (var orderId in orderIds)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ITripBookingExpiryService>().ExpireAsync(orderId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job hết hạn giữ chỗ lỗi ở đơn {OrderId}", orderId);
            }
        }
    }
}
