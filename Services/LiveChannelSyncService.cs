using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Services
{
    public class LiveChannelSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _db;
        private readonly ILogger<LiveChannelSyncService> _logger;

        private const string BaseUrl = "https://raw.githubusercontent.com/famelack/famelack-data/main/tv/raw/categories/";
        private static readonly string[] CategoryFiles = 
        {
            "animation", "educational", "entertainment", "kids", "lifestyle", 
            "movies", "music", "news", "religious", "series", "sports"
        };

        private const int BatchSize = 500;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public LiveChannelSyncService(HttpClient httpClient, AppDbContext db, ILogger<LiveChannelSyncService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
        }

        public async Task SyncAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting multi-file LiveChannel sync from Famelack...");

            try
            {
                var channelsDict = new Dictionary<string, LiveChannel>();

                // 1. Process category-specific files
                foreach (var catFile in CategoryFiles)
                {
                    var url = $"{BaseUrl}{catFile}.json";
                    var categoryLabel = char.ToUpper(catFile[0]) + catFile[1..];
                    
                    _logger.LogInformation("Processing category: {Category} from {Url}", categoryLabel, url);
                    await ProcessFileAsync(url, categoryLabel, channelsDict, ct);
                }

                // 2. Process 'all.json' to ensure we have any missing channels, tagged as 'General' if no other category
                _logger.LogInformation("Processing 'all.json' for remaining channels...");
                await ProcessFileAsync($"{BaseUrl}all.json", "General", channelsDict, ct);

                // 3. Upsert collected channels in batches
                var allChannels = channelsDict.Values.ToList();
                _logger.LogInformation("Collected {Count} unique channels. Starting batch upsert...", allChannels.Count);

                for (int i = 0; i < allChannels.Count; i += BatchSize)
                {
                    var batch = allChannels.Skip(i).Take(BatchSize).ToList();
                    await UpsertBatchAsync(batch, ct);
                    _logger.LogInformation("Upserted {Processed}/{Total} channels...", Math.Min(i + BatchSize, allChannels.Count), allChannels.Count);
                }

                _logger.LogInformation("Sync complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during LiveChannel synchronization.");
                throw;
            }
        }

        private async Task ProcessFileAsync(string url, string categoryLabel, Dictionary<string, LiveChannel> channelsDict, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch {Url}: {StatusCode}", url, response.StatusCode);
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<FamelackChannelDto>(stream, _jsonOptions, ct))
                {
                    if (item == null || item.Stream_Urls == null || item.Stream_Urls.Count == 0 || string.IsNullOrWhiteSpace(item.Nanoid))
                        continue;

                    if (!channelsDict.TryGetValue(item.Nanoid, out var channel))
                    {
                        channel = new LiveChannel
                        {
                            Id = item.Nanoid,
                            Name = item.Name ?? "Unknown",
                            LogoUrl = null,
                            StreamUrl = item.Stream_Urls[0],
                            Country = item.Country?.ToUpper(),
                            Languages = item.Languages ?? new(),
                            Categories = new List<string>(),
                            LastUpdated = DateTime.UtcNow
                        };
                        channelsDict[item.Nanoid] = channel;
                    }

                    var cats = channel.Categories;
                    if (!cats.Contains(categoryLabel))
                    {
                        cats.Add(categoryLabel);
                        channel.Categories = cats; // Trigger setter to update CategoriesRaw
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {Url}", url);
            }
        }

        private async Task UpsertBatchAsync(List<LiveChannel> batch, CancellationToken ct)
        {
            var ids = batch.Select(b => b.Id).ToList();
            var existingChannels = await _db.LiveChannels
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct);

            foreach (var channel in batch)
            {
                if (existingChannels.TryGetValue(channel.Id, out var existing))
                {
                    // Update
                    existing.Name = channel.Name;
                    existing.LogoUrl = channel.LogoUrl;
                    existing.StreamUrl = channel.StreamUrl;
                    existing.Country = channel.Country;
                    existing.Languages = channel.Languages;
                    
                    // Merge categories: keep existing ones and add new ones
                    var existingCats = existing.Categories;
                    var newCats = channel.Categories;
                    var mergedCats = existingCats.Union(newCats, StringComparer.OrdinalIgnoreCase).ToList();
                    
                    // Always re-assign to trigger the setter and update CategoriesRaw string
                    existing.Categories = mergedCats;
                    
                    // Explicitly mark as modified
                    _db.Entry(existing).Property(c => c.CategoriesRaw).IsModified = true;
                    
                    existing.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    // Add
                    await _db.LiveChannels.AddAsync(channel, ct);
                }
            }

            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        private class FamelackChannelDto
        {
            [JsonPropertyName("nanoid")]
            public string? Nanoid { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("stream_urls")]
            public List<string>? Stream_Urls { get; set; }

            [JsonPropertyName("country")]
            public string? Country { get; set; }

            [JsonPropertyName("languages")]
            public List<string>? Languages { get; set; }
        }
    }
}
