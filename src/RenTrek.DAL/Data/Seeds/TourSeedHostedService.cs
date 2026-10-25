using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RenTrek.DAL.Data.Seeds;

/// <summary>
/// Tự động chạy seed tour khi ứng dụng khởi động trong môi trường Development.
/// </summary>
internal sealed class TourSeedHostedService(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuredValue = configuration["Seed:Tours"];
        var seedEnabled = !bool.TryParse(configuredValue, out var parsedValue) || parsedValue;
        if (!environment.IsDevelopment() || !seedEnabled)
            return;

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TourSeedData.InitializeAsync(context);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
