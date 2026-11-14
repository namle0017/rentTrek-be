using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.BackgroundServices;

/// <summary>
/// Mỗi 15 phút chạy vòng đời đơn thuê: phí trễ nhận / no-show, nhắc trả, phạt trả trễ, nghi mất, tự xác nhận nhận hàng,
/// nhắc nhập STK, huỷ gia hạn chưa thanh toán. Mỗi việc chạy trong scope (DbContext) riêng để lỗi một đơn không lan sang đơn khác.
/// Dùng TimeProvider của DI nên đồng hồ giả lập ở Development cũng áp dụng.
/// </summary>
public sealed class RentalLifecycleBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RentalLifecycleBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

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
        IReadOnlyList<RentalLifecycleWorkItem> work;
        try
        {
            using var scope = scopeFactory.CreateScope();
            work = await scope.ServiceProvider.GetRequiredService<IRentalLifecycleService>().CollectWorkAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Job vòng đời đơn thuê: không gom được việc cần làm");
            return;
        }

        foreach (var item in work)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IRentalLifecycleService>().ProcessAsync(item, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job vòng đời đơn thuê lỗi ở {Kind} {Id}", item.Kind, item.Id);
            }
        }
    }
}
