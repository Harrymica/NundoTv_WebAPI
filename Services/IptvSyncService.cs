using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Services
{
    public class IptvSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _db;
        private readonly ILogger<IptvSyncService> _logger;

        private const string ChannelsUrl = "https://iptv-org.github.io/api/channels.json";
        private const string StreamsUrl = "https://iptv-org.github.io/api/streams.json";
        private const string BlocklistUrl = "https://iptv-org.github.io/api/blocklist.json";
        private const string LogosUrl = "https://iptv-org.github.io/api/logos.json";

        private const int BatchSize = 500;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,   // DTOs use [JsonPropertyName]
            AllowTrailingCommas = true
        };

        public IptvSyncService(HttpClient httpClient, AppDbContext db, ILogger<IptvSyncService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Fetches channels, streams, and blocklist from the iptv-org API using
        /// streaming JSON deserialization to keep memory usage low, then persists
        /// everything to the database in small batches.
        /// </summary>
        public async Task<SyncResultDto> SyncAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting IPTV sync (streaming mode)...");

            // ── 1. Stream blocklist into a dictionary (small dataset, ~200 entries) ──
            var blocklistLookup = await StreamBlocklistToDictionaryAsync(ct);
            _logger.LogInformation("Loaded {Count} blocklist entries via streaming", blocklistLookup.Count);

            // ── 2. Stream streams into a channel→URL dictionary ──
            var streamLookup = await StreamStreamsToDictionaryAsync(ct);
            _logger.LogInformation("Loaded {Count} stream mappings via streaming", streamLookup.Count);

            // ── 3. Load custom blocked keywords from DB ──
            var keywords = await _db.BlockedKeywords
                .Select(k => k.Keyword.ToLower())
                .ToListAsync(ct);

            // ── 4. Clear existing data with TRUNCATE (instant, no entity loading) ──
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "TRUNCATE TABLE \"BlockedChannels\", \"Channels\" RESTART IDENTITY", ct);

                // Disable change tracking for bulk inserts
                _db.ChangeTracker.AutoDetectChangesEnabled = false;

                // ── 5. Stream channels, classify, and batch-insert ──
                int totalFetched = 0;
                int storedCount = 0;
                int blockedCount = 0;
                int streamsMatched = 0;

                var cleanBatch = new List<Channel>(BatchSize);
                var blockedBatch = new List<BlockedChannel>(BatchSize);

                await foreach (var ch in StreamJsonArrayAsync<IptvChannelDto>(ChannelsUrl, ct))
                {
                    totalFetched++;

                    string? blockReason = null;
                    string? blockRef = null;

                    // Check the iptv-org blocklist
                    if (blocklistLookup.TryGetValue(ch.Id, out var entry))
                    {
                        blockReason = entry.Reason;
                        blockRef = entry.Ref;
                    }
                    // Check is_nsfw flag on the channel itself
                    else if (ch.IsNsfw)
                    {
                        blockReason = "nsfw";
                    }
                    // Check custom keyword matches
                    else if (MatchesBlockedKeyword(ch.Name, keywords))
                    {
                        blockReason = "keyword_match";
                    }

                    streamLookup.TryGetValue(ch.Id, out var streamUrl);

                    if (streamUrl != null) streamsMatched++;

                    if (blockReason != null)
                    {
                        blockedBatch.Add(new BlockedChannel
                        {
                            IptvId = ch.Id,
                            Name = ch.Name,
                            AltNames = ch.AltNames ?? new(),
                            Network = ch.Network,
                            Owners = ch.Owners ?? new(),
                            Country = ch.Country,
                            Categories = ch.Categories ?? new(),
                            IsNsfw = ch.IsNsfw,
                            Launched = ch.Launched,
                            Closed = ch.Closed,
                            ReplacedBy = ch.ReplacedBy,
                            Website = ch.Website,
                            StreamUrl = streamUrl,
                            BlockReason = blockReason,
                            BlockRef = blockRef,
                            BlockedAt = DateTime.UtcNow
                        });
                        blockedCount++;

                        if (blockedBatch.Count >= BatchSize)
                        {
                            await FlushBlockedBatchAsync(blockedBatch, ct);
                        }
                    }
                    else
                    {
                        cleanBatch.Add(new Channel
                        {
                            IptvId = ch.Id,
                            Name = ch.Name,
                            AltNames = ch.AltNames ?? new(),
                            Network = ch.Network,
                            Owners = ch.Owners ?? new(),
                            Country = ch.Country,
                            Categories = ch.Categories ?? new(),
                            IsNsfw = ch.IsNsfw,
                            Launched = ch.Launched,
                            Closed = ch.Closed,
                            ReplacedBy = ch.ReplacedBy,
                            Website = ch.Website,
                            StreamUrl = streamUrl,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                        storedCount++;

                        if (cleanBatch.Count >= BatchSize)
                        {
                            await FlushCleanBatchAsync(cleanBatch, ct);
                        }
                    }

                    // Log progress every 5000 channels
                    if (totalFetched % 5000 == 0)
                    {
                        _logger.LogInformation(
                            "Sync progress: {Fetched} channels processed so far...", totalFetched);
                    }
                }

                // Flush remaining items in the last partial batches
                if (cleanBatch.Count > 0)
                    await FlushCleanBatchAsync(cleanBatch, ct);

                if (blockedBatch.Count > 0)
                    await FlushBlockedBatchAsync(blockedBatch, ct);

                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Sync complete: {Total} total, {Clean} stored, {Blocked} blocked, {Streams} streams matched",
                    totalFetched, storedCount, blockedCount, streamsMatched);

                return new SyncResultDto
                {
                    TotalFetched = totalFetched,
                    Stored = storedCount,
                    Blocked = blockedCount,
                    StreamsMatched = streamsMatched,
                    Status = "completed",
                    SyncedAt = DateTime.UtcNow
                };
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            finally
            {
                // Restore change tracking and release any tracked entities
                _db.ChangeTracker.AutoDetectChangesEnabled = true;
                _db.ChangeTracker.Clear();

                // Hint GC to reclaim the transient buffers
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }
        }

        // ─── Batch flush helpers ────────────────────────────────────────

        private async Task FlushCleanBatchAsync(List<Channel> batch, CancellationToken ct)
        {
            await _db.Channels.AddRangeAsync(batch, ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();           // release tracked entities immediately
            batch.Clear();
        }

        private async Task FlushBlockedBatchAsync(List<BlockedChannel> batch, CancellationToken ct)
        {
            await _db.BlockedChannels.AddRangeAsync(batch, ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            batch.Clear();
        }

        // ─── Logo sync ──────────────────────────────────────────────────

        /// <summary>
        /// Fetches logos from the iptv-org API, matches them to existing channels
        /// and blocked channels, then stores them in separate logo tables.
        /// Must be called AFTER SyncAsync so channels exist in the database.
        /// </summary>
        public async Task<LogoSyncResultDto> SyncLogosAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting logo sync (streaming mode)...");

            // ── 1. Build IptvId → DB Id lookups from both tables ──
            var channelLookup = await _db.Channels
                .AsNoTracking()
                .ToDictionaryAsync(c => c.IptvId, c => c.Id, ct);

            var blockedLookup = await _db.BlockedChannels
                .AsNoTracking()
                .ToDictionaryAsync(c => c.IptvId, c => c.Id, ct);

            _logger.LogInformation("Loaded {Clean} channel IDs and {Blocked} blocked channel IDs for logo matching",
                channelLookup.Count, blockedLookup.Count);

            // ── 2. Clear existing logo data ──
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "TRUNCATE TABLE \"BlockedChannelLogos\", \"ChannelLogos\" RESTART IDENTITY", ct);

                _db.ChangeTracker.AutoDetectChangesEnabled = false;

                // ── 3. Stream logos, classify, and batch-insert ──
                int totalFetched = 0;
                int channelLogosStored = 0;
                int blockedLogosStored = 0;
                int unmatched = 0;

                var cleanLogoBatch = new List<ChannelLogo>(BatchSize);
                var blockedLogoBatch = new List<BlockedChannelLogo>(BatchSize);

                await foreach (var logo in StreamJsonArrayAsync<IptvLogoDto>(LogosUrl, ct))
                {
                    totalFetched++;

                    if (channelLookup.TryGetValue(logo.Channel, out var channelId))
                    {
                        cleanLogoBatch.Add(new ChannelLogo
                        {
                            ChannelId = channelId,
                            IptvChannelId = logo.Channel,
                            Url = logo.Url,
                            Width = logo.Width,
                            Height = logo.Height,
                            Format = logo.Format,
                            Tags = logo.Tags ?? new(),
                            InUse = logo.InUse,
                            Feed = logo.Feed
                        });
                        channelLogosStored++;

                        if (cleanLogoBatch.Count >= BatchSize)
                            await FlushChannelLogoBatchAsync(cleanLogoBatch, ct);
                    }
                    else if (blockedLookup.TryGetValue(logo.Channel, out var blockedId))
                    {
                        blockedLogoBatch.Add(new BlockedChannelLogo
                        {
                            BlockedChannelId = blockedId,
                            IptvChannelId = logo.Channel,
                            Url = logo.Url,
                            Width = logo.Width,
                            Height = logo.Height,
                            Format = logo.Format,
                            Tags = logo.Tags ?? new(),
                            InUse = logo.InUse,
                            Feed = logo.Feed
                        });
                        blockedLogosStored++;

                        if (blockedLogoBatch.Count >= BatchSize)
                            await FlushBlockedChannelLogoBatchAsync(blockedLogoBatch, ct);
                    }
                    else
                    {
                        unmatched++;
                    }

                    if (totalFetched % 5000 == 0)
                    {
                        _logger.LogInformation(
                            "Logo sync progress: {Fetched} logos processed so far...", totalFetched);
                    }
                }

                // Flush remaining partial batches
                if (cleanLogoBatch.Count > 0)
                    await FlushChannelLogoBatchAsync(cleanLogoBatch, ct);

                if (blockedLogoBatch.Count > 0)
                    await FlushBlockedChannelLogoBatchAsync(blockedLogoBatch, ct);

                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Logo sync complete: {Total} total, {Clean} channel logos, {Blocked} blocked logos, {Unmatched} unmatched",
                    totalFetched, channelLogosStored, blockedLogosStored, unmatched);

                return new LogoSyncResultDto
                {
                    TotalFetched = totalFetched,
                    ChannelLogosStored = channelLogosStored,
                    BlockedChannelLogosStored = blockedLogosStored,
                    Unmatched = unmatched,
                    Status = "completed",
                    SyncedAt = DateTime.UtcNow
                };
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            finally
            {
                _db.ChangeTracker.AutoDetectChangesEnabled = true;
                _db.ChangeTracker.Clear();
                GC.Collect(2, GCCollectionMode.Optimized, false);
            }
        }

        private async Task FlushChannelLogoBatchAsync(List<ChannelLogo> batch, CancellationToken ct)
        {
            await _db.ChannelLogos.AddRangeAsync(batch, ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            batch.Clear();
        }

        private async Task FlushBlockedChannelLogoBatchAsync(List<BlockedChannelLogo> batch, CancellationToken ct)
        {
            await _db.BlockedChannelLogos.AddRangeAsync(batch, ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            batch.Clear();
        }

        // ─── Streaming JSON helpers ─────────────────────────────────────

        /// <summary>
        /// Streams a JSON array from a URL, yielding one deserialized item at a time
        /// without ever holding the entire array in memory.
        /// </summary>
        private async IAsyncEnumerable<T> StreamJsonArrayAsync<T>(
            string url,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            _logger.LogInformation("Streaming {Url}...", url);

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<T>(stream, _jsonOptions, ct))
            {
                if (item is not null)
                    yield return item;
            }
        }

        /// <summary>
        /// Streams the blocklist JSON into a dictionary keyed by channel ID.
        /// </summary>
        private async Task<Dictionary<string, IptvBlocklistEntryDto>> StreamBlocklistToDictionaryAsync(CancellationToken ct)
        {
            var dict = new Dictionary<string, IptvBlocklistEntryDto>(256);

            await foreach (var entry in StreamJsonArrayAsync<IptvBlocklistEntryDto>(BlocklistUrl, ct))
            {
                dict.TryAdd(entry.Channel, entry);
            }

            return dict;
        }

        /// <summary>
        /// Streams the streams JSON into a dictionary: channel ID → first stream URL.
        /// </summary>
        private async Task<Dictionary<string, string>> StreamStreamsToDictionaryAsync(CancellationToken ct)
        {
            var dict = new Dictionary<string, string>(16_000);

            await foreach (var s in StreamJsonArrayAsync<IptvStreamDto>(StreamsUrl, ct))
            {
                if (!string.IsNullOrEmpty(s.Channel))
                {
                    dict.TryAdd(s.Channel!, s.Url);   // keep the first stream per channel
                }
            }

            return dict;
        }

        /// <summary>
        /// Check if a channel name contains any of the blocked keywords (case-insensitive).
        /// </summary>
        private static bool MatchesBlockedKeyword(string channelName, List<string> keywords)
        {
            var nameLower = channelName.ToLower();
            return keywords.Any(kw => nameLower.Contains(kw));
        }
    }
}
