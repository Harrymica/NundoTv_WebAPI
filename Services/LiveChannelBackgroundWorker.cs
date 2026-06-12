using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NundoTv_WebAPI.Services
{
    public class LiveChannelBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LiveChannelBackgroundWorker> _logger;

        public LiveChannelBackgroundWorker(IServiceProvider serviceProvider, ILogger<LiveChannelBackgroundWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LiveChannel Background Worker is starting.");

            // Run once on startup after a short delay
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting daily LiveChannel synchronization task.");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<LiveChannelSyncService>();
                        await syncService.SyncAsync(stoppingToken);
                    }

                    _logger.LogInformation("Daily LiveChannel synchronization task completed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during daily LiveChannel synchronization.");
                }

                // Wait for 24 hours
                _logger.LogInformation("Next sync scheduled in 24 hours.");
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
