using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Services
{
    public class LivePremiumChannelSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _db;
        private readonly ILogger<LivePremiumChannelSyncService> _logger;

        private const string BaseUrl = "https://raw.githubusercontent.com/famelack/famelack-data/main/tv/raw/countries/";
        
        // Comprehensive list of countries from famelack-data repo structure
        private static readonly string[] CountryCodes = 
        {
            "ad", "ae", "af", "ag", "ai", "al", "am", "ao", "ar", "as", "at", "au", "aw", "ax", "az",
            "ba", "bb", "bd", "be", "bf", "bg", "bh", "bi", "bj", "bm", "bn", "bo", "br", "bs", "bt", "bw", "by", "bz",
            "ca", "cd", "cf", "cg", "ch", "ci", "ck", "cl", "cm", "cn", "co", "cr", "cu", "cv", "cw", "cy", "cz",
            "de", "dj", "dk", "dm", "do", "dz",
            "ec", "ee", "eg", "er", "es", "et",
            "fi", "fj", "fk", "fm", "fo", "fr",
            "ga", "gb", "gd", "ge", "gf", "gg", "gh", "gi", "gl", "gm", "gn", "gp", "gq", "gr", "gt", "gu", "gw", "gy",
            "hk", "hn", "hr", "ht", "hu",
            "id", "ie", "il", "im", "in", "io", "iq", "ir", "is", "it",
            "je", "jm", "jo", "jp",
            "ke", "kg", "kh", "ki", "km", "kn", "kp", "kr", "kw", "ky", "kz",
            "la", "lb", "lc", "li", "lk", "lr", "ls", "lt", "lu", "lv", "ly",
            "ma", "mc", "md", "me", "mf", "mg", "mh", "mk", "ml", "mm", "mn", "mo", "mp", "mq", "mr", "ms", "mt", "mu", "mv", "mw", "mx", "my", "mz",
            "na", "nc", "ne", "nf", "ng", "ni", "nl", "no", "np", "nr", "nu", "nz",
            "om",
            "pa", "pe", "pf", "pg", "ph", "pk", "pl", "pm", "pn", "pr", "ps", "pt", "pw", "py",
            "qa",
            "re", "ro", "rs", "ru", "rw",
            "sa", "sb", "sc", "sd", "se", "sg", "sh", "si", "sj", "sk", "sl", "sm", "sn", "so", "sr", "ss", "st", "sv", "sx", "sy", "sz",
            "tc", "td", "tg", "th", "tj", "tk", "tl", "tm", "tn", "to", "tr", "tt", "tv", "tw", "tz",
            "ua", "ug", "um", "us", "uy", "uz",
            "va", "vc", "ve", "vg", "vi", "vn", "vu",
            "wf", "ws",
            "xk",
            "ye", "yt",
            "za", "zm", "zw"
        };

        private const int BatchSize = 500;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public LivePremiumChannelSyncService(HttpClient httpClient, AppDbContext db, ILogger<LivePremiumChannelSyncService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
        }

        public async Task SyncAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting multi-country LivePremiumChannel sync from Famelack...");

            try
            {
                var channelsDict = new Dictionary<string, LivePremiumChannel>();

                foreach (var countryCode in CountryCodes)
                {
                    var url = $"{BaseUrl}{countryCode}.json";
                    _logger.LogDebug("Processing country: {Country} from {Url}", countryCode.ToUpper(), url);
                    await ProcessFileAsync(url, countryCode.ToUpper(), channelsDict, ct);
                }

                var allChannels = channelsDict.Values.ToList();
                _logger.LogInformation("Collected {Count} unique premium channels. Starting batch upsert...", allChannels.Count);

                for (int i = 0; i < allChannels.Count; i += BatchSize)
                {
                    var batch = allChannels.Skip(i).Take(BatchSize).ToList();
                    await UpsertBatchAsync(batch, ct);
                    _logger.LogInformation("Upserted {Processed}/{Total} premium channels...", Math.Min(i + BatchSize, allChannels.Count), allChannels.Count);
                }

                _logger.LogInformation("Premium Channel Sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during LivePremiumChannel synchronization.");
                throw;
            }
        }

        private async Task ProcessFileAsync(string url, string countryLabel, Dictionary<string, LivePremiumChannel> channelsDict, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    // Many small countries might not have a file, just skip silently for them unless it's a major one
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return;
                    
                    _logger.LogWarning("Failed to fetch {Url}: {StatusCode}", url, response.StatusCode);
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<FamelackPremiumChannelDto>(stream, _jsonOptions, ct))
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Nanoid))
                        continue;

                    // Some channels only have stream URLs, others only have YouTube, some have neither.
                    // We only want channels that can actually play SOMETHING.
                    bool hasStream = item.Stream_Urls != null && item.Stream_Urls.Count > 0;
                    bool hasYoutube = item.Youtube_Urls != null && item.Youtube_Urls.Count > 0;

                    if (!hasStream && !hasYoutube)
                        continue;

                    if (!channelsDict.TryGetValue(item.Nanoid, out var channel))
                    {
                        channel = new LivePremiumChannel
                        {
                            Id = item.Nanoid,
                            Name = item.Name ?? "Unknown",
                            LogoUrl = null,
                            StreamUrl = hasStream ? item.Stream_Urls![0] : string.Empty,
                            YoutubeUrl = hasYoutube ? item.Youtube_Urls![0] : null,
                            Country = countryLabel,
                            Languages = item.Languages ?? new(),
                            Categories = new List<string> { "General" }, // Countries file doesn't have categories, default to General
                            LastUpdated = DateTime.UtcNow
                        };
                        channelsDict[item.Nanoid] = channel;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {Url}", url);
            }
        }

        private async Task UpsertBatchAsync(List<LivePremiumChannel> batch, CancellationToken ct)
        {
            var ids = batch.Select(b => b.Id).ToList();
            var existingChannels = await _db.LivePremiumChannels
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

            foreach (var channel in batch)
            {
                if (existingChannels.TryGetValue(channel.Id, out var existing))
                {
                    existing.Name = channel.Name;
                    existing.LogoUrl = channel.LogoUrl;
                    existing.StreamUrl = channel.StreamUrl;
                    existing.YoutubeUrl = channel.YoutubeUrl;
                    existing.Country = channel.Country;
                    existing.Languages = channel.Languages;
                    existing.Categories = channel.Categories;
                    existing.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    await _db.LivePremiumChannels.AddAsync(channel, ct);
                }
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        private class FamelackPremiumChannelDto
        {
            [JsonPropertyName("nanoid")]
            public string? Nanoid { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("stream_urls")]
            public List<string>? Stream_Urls { get; set; }

            [JsonPropertyName("youtube_urls")]
            public List<string>? Youtube_Urls { get; set; }

            [JsonPropertyName("country")]
            public string? Country { get; set; }

            [JsonPropertyName("languages")]
            public List<string>? Languages { get; set; }
        }
    }
}
