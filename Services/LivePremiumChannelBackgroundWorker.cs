namespace NundoTv_WebAPI.Services
{
    public class LivePremiumChannelBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LivePremiumChannelBackgroundWorker> _logger;
        private readonly TimeSpan _syncInterval = TimeSpan.FromHours(6); 

        public LivePremiumChannelBackgroundWorker(IServiceProvider serviceProvider, ILogger<LivePremiumChannelBackgroundWorker> _logger)
        {
            _serviceProvider = serviceProvider;
            this._logger = _logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LivePremiumChannel Background Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting scheduled LivePremiumChannel synchronization...");
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var syncService = scope.ServiceProvider.GetRequiredService<LivePremiumChannelSyncService>();
                        await syncService.SyncAsync(stoppingToken);
                    }
                    _logger.LogInformation("Scheduled LivePremiumChannel synchronization finished.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scheduled LivePremiumChannel synchronization.");
                }

                await Task.Delay(_syncInterval, stoppingToken);
            }
        }
    }
}
