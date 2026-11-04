using RenTrek.BLL.Services.Interfaces;

namespace RenTrek.API.BackgroundServices;

/// <summary>
/// Mỗi giờ xóa payout details đã hết hạn lưu trữ.
/// </summary>
public sealed class RefundDataPurgeBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RefundDataPurgeBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                // Xóa dữ liệu là không hoàn tác → luôn theo giờ thật, không theo đồng hồ giả lập (Development)
                var refundCases = ActivatorUtilities.CreateInstance<RefundCaseService>(scope.ServiceProvider, TimeProvider.System);
                await refundCases.PurgeSensitiveDataAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<IRefundCaseService>().PurgeSensitiveDataAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Job xóa dữ liệu hoàn tiền thất bại");
            }
        }
    }
}
