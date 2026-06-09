using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Services
{
    public class ImerlSyncService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImerlSyncService> _logger;
        private readonly HttpClient _httpClient;

        public ImerlSyncService(IServiceProvider serviceProvider, ILogger<ImerlSyncService> logger, HttpClient httpClient)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<object> SyncAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting iMerl sync...");
            
            var playlistUrl = "https://raw.githubusercontent.com/iMerl/Free-IPTV/master/playlist.m3u8";
            var response = await _httpClient.GetStringAsync(playlistUrl, ct);

            var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var channels = new List<IMerlChannel>();
            
            IMerlChannel? currentChannel = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("#EXTINF:"))
                {
                    currentChannel = new IMerlChannel();
                    
                    // Parse tvg-logo
                    var logoMatch = Regex.Match(line, @"tvg-logo=""([^""]+)""");
                    if (logoMatch.Success) currentChannel.Logo = logoMatch.Groups[1].Value;

                    // Parse group-title (Category)
                    var groupMatch = Regex.Match(line, @"group-title=""([^""]+)""");
                    if (groupMatch.Success) currentChannel.Category = groupMatch.Groups[1].Value;

                    // Parse tvg-country (Country)
                    var countryMatch = Regex.Match(line, @"tvg-country=""([^""]+)""");
                    if (countryMatch.Success) currentChannel.Country = countryMatch.Groups[1].Value;

                    // Parse name
                    var commaIndex = line.LastIndexOf(',');
                    if (commaIndex >= 0 && commaIndex < line.Length - 1)
                    {
                        currentChannel.Name = line.Substring(commaIndex + 1).Trim();
                    }
                    else
                    {
                        var tvgNameMatch = Regex.Match(line, @"tvg-name=""([^""]+)""");
                        currentChannel.Name = tvgNameMatch.Success ? tvgNameMatch.Groups[1].Value : "Unknown";
                    }
                }
                else if (!line.StartsWith("#") && currentChannel != null)
                {
                    currentChannel.StreamUrl = line.Trim();
                    
                    // Only add if not empty
                    if (!string.IsNullOrWhiteSpace(currentChannel.StreamUrl))
                    {
                        channels.Add(currentChannel);
                    }
                    currentChannel = null;
                }
            }

            // Save to DB
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _logger.LogInformation("Deleting existing iMerl channels...");
            
            try
            {
                await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ImerlChannels\" RESTART IDENTITY", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to truncate, trying to delete normally...");
                db.ImerlChannels.RemoveRange(db.ImerlChannels);
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation($"Inserting {channels.Count} iMerl channels...");
            await db.ImerlChannels.AddRangeAsync(channels, ct);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("iMerl sync completed successfully!");

            return new
            {
                Success = true,
                ChannelsInserted = channels.Count
            };
        }
    }
}
