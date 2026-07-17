using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using System.Xml;
using System.IO.Compression;

namespace NundoTv_WebAPI.Services
{
    public class EpgSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EpgSyncService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private static readonly string[] DefaultFeeds = new[]
        {
            "https://epg.pw/xmltv/epg_US.xml",
            "https://epg.pw/xmltv/epg_GB.xml"
        };

        public EpgSyncService(IServiceProvider serviceProvider, ILogger<EpgSyncService> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay to let other services start first
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("EPG Sync started...");
                try
                {
                    var feedUrls = _configuration.GetSection("Epg:FeedUrls").Get<string[]>() ?? DefaultFeeds;

                    foreach (var url in feedUrls)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        try
                        {
                            await SyncEpgDataAsync(url, stoppingToken);
                            _logger.LogInformation("EPG feed synced: {Url}", url);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to sync EPG feed: {Url}, skipping...", url);
                        }
                    }

                    _logger.LogInformation("EPG Sync completed. Next run in 12 hours.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while syncing EPG schedules.");
                }

                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }

        private async Task SyncEpgDataAsync(string url, CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _logger.LogInformation($"Downloading EPG data from {url}");
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) 
            {
                _logger.LogWarning($"EPG data not found at {url}");
                return;
            }
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);
            
            using Stream readStream = url.EndsWith(".gz") ? new GZipStream(stream, CompressionMode.Decompress) : stream;

            var settings = new XmlReaderSettings
            {
                Async = true,
                IgnoreWhitespace = true,
                IgnoreComments = true,
                DtdProcessing = DtdProcessing.Ignore
            };

            using var reader = XmlReader.Create(readStream, settings);

            var epgPrograms = new List<EpgProgram>();
            var channelNames = new Dictionary<string, string>();
            
            // Delete programs older than yesterday
            var yesterday = DateTime.UtcNow.AddDays(-1);
            await db.EpgPrograms.Where(p => p.Stop < yesterday).ExecuteDeleteAsync(stoppingToken);

            while (await reader.ReadAsync())
            {
                if (stoppingToken.IsCancellationRequested) break;

                if (reader.NodeType == XmlNodeType.Element && reader.Name == "channel")
                {
                    var id = reader.GetAttribute("id");
                    if (!string.IsNullOrEmpty(id))
                    {
                        using var subReader = reader.ReadSubtree();
                        while (await subReader.ReadAsync())
                        {
                            if (subReader.NodeType == XmlNodeType.Element && subReader.Name == "display-name")
                            {
                                channelNames[id] = await subReader.ReadElementContentAsStringAsync();
                                break;
                            }
                        }
                    }
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
                {
                    var channelId = reader.GetAttribute("channel");
                    var startStr = reader.GetAttribute("start");
                    var stopStr = reader.GetAttribute("stop");

                    if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(startStr) || string.IsNullOrEmpty(stopStr))
                        continue;

                    var program = new EpgProgram
                    {
                        ChannelId = channelId,
                        ChannelName = channelNames.TryGetValue(channelId, out var name) ? name : channelId,
                        Start = ParseXmlTvDate(startStr),
                        Stop = ParseXmlTvDate(stopStr)
                    };

                    using var subReader = reader.ReadSubtree();
                    while (await subReader.ReadAsync())
                    {
                        if (subReader.NodeType == XmlNodeType.Element)
                        {
                            if (subReader.Name == "title")
                                program.Title = await subReader.ReadElementContentAsStringAsync();
                            else if (subReader.Name == "desc")
                                program.Description = await subReader.ReadElementContentAsStringAsync();
                            else if (subReader.Name == "category")
                                program.Category = await subReader.ReadElementContentAsStringAsync();
                            else if (subReader.Name == "icon")
                            {
                                program.IconUrl = subReader.GetAttribute("src");
                                await subReader.ReadAsync(); // Advance past the empty icon element
                            }
                        }
                    }

                    epgPrograms.Add(program);

                    // Batch save every 2000 records
                    if (epgPrograms.Count >= 2000)
                    {
                        await UpsertBatch(db, epgPrograms, stoppingToken);
                        epgPrograms.Clear();
                    }
                }
            }
            
            if (epgPrograms.Count > 0)
            {
                await UpsertBatch(db, epgPrograms, stoppingToken);
            }
        }

        private async Task UpsertBatch(AppDbContext db, List<EpgProgram> programs, CancellationToken stoppingToken)
        {
            var channelIds = programs.Select(p => p.ChannelId).Distinct().ToList();
            var minStart = programs.Min(p => p.Start);
            var maxStart = programs.Max(p => p.Start);

            // Using ExecuteDeleteAsync for better performance
            await db.EpgPrograms
                .Where(p => channelIds.Contains(p.ChannelId) && p.Start >= minStart && p.Start <= maxStart)
                .ExecuteDeleteAsync(stoppingToken);
            
            await db.EpgPrograms.AddRangeAsync(programs, stoppingToken);
            await db.SaveChangesAsync(stoppingToken);
        }

        private DateTime ParseXmlTvDate(string dateStr)
        {
            try
            {
                // XMLTV date format: 20240409060000 +0000
                if (dateStr.Length >= 14)
                {
                    int year = int.Parse(dateStr.Substring(0, 4));
                    int month = int.Parse(dateStr.Substring(4, 2));
                    int day = int.Parse(dateStr.Substring(6, 2));
                    int hour = int.Parse(dateStr.Substring(8, 2));
                    int min = int.Parse(dateStr.Substring(10, 2));
                    int sec = int.Parse(dateStr.Substring(12, 2));
                    
                    var dt = new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc);
                    
                    if (dateStr.Length >= 20 && (dateStr[15] == '+' || dateStr[15] == '-'))
                    {
                        var offsetSign = dateStr[15] == '+' ? -1 : 1;
                        var offsetHour = int.Parse(dateStr.Substring(16, 2));
                        var offsetMin = int.Parse(dateStr.Substring(18, 2));
                        
                        dt = dt.AddHours(offsetSign * offsetHour).AddMinutes(offsetSign * offsetMin);
                    }
                    
                    return dt;
                }
            }
            catch { }
            return DateTime.UtcNow;
        }
    }
}
