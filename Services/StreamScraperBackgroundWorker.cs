using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Services
{
    public class StreamScraperBackgroundWorker : BackgroundService
    {
        private readonly IStreamScraperService _scraperService;
        private readonly ILogger<StreamScraperBackgroundWorker> _logger;

        public StreamScraperBackgroundWorker(
            IStreamScraperService scraperService,
            ILogger<StreamScraperBackgroundWorker> logger)
        {
            _scraperService = scraperService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StreamScraperBackgroundWorker initiated.");

            // Initial sync on startup
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            await _scraperService.SyncAndCheckStreamsAsync(stoppingToken);

            // Periodic 12-hour sync
            using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogInformation("Running scheduled 12-hour Stream Scraper sync...");
                await _scraperService.SyncAndCheckStreamsAsync(stoppingToken);
            }
        }
    }
}
