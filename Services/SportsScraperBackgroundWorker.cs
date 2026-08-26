using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NundoTv_WebAPI.Services
{
    public class SportsScraperBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SportsScraperBackgroundWorker> _logger;

        public SportsScraperBackgroundWorker(IServiceProvider serviceProvider, ILogger<SportsScraperBackgroundWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SportsScraper Background Worker is starting.");

            // Wait 5 seconds on startup before running first scrape
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Executing periodic Live Sports match scraping run...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var scraperService = scope.ServiceProvider.GetRequiredService<SportsScraperService>();
                        await scraperService.ScrapeAndSyncMatchesAsync(stoppingToken);
                    }

                    _logger.LogInformation("Live Sports match scraping finished successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Live Sports match scraping run.");
                }

                // Poll every 5 minutes to keep live sports scores and match streams up to date
                _logger.LogInformation("Next Live Sports scrape scheduled in 5 minutes.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
