using Erp.Modules.Treasury.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Treasury.Infrastructure.Jobs;

public class ExchangeRateRefreshJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExchangeRateRefreshJob> _logger;

    public ExchangeRateRefreshJob(IServiceProvider serviceProvider, ILogger<ExchangeRateRefreshJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Run at 07:00 UTC daily
            var nextRun = now.Date.AddDays(1).AddHours(7);
            if (now.Hour == 7 && now.Minute < 5)
                nextRun = now;

            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var rateService = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();
                await rateService.RefreshRatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExchangeRateRefreshJob failed");
            }
        }
    }
}
