using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Services
{
    public interface IStreamScraperService
    {
        Task SyncAndCheckStreamsAsync(CancellationToken cancellationToken = default);
    }

    public class StreamScraperService : IStreamScraperService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StreamScraperService> _logger;

        private const string WikiSportsUrl = "https://raw.githubusercontent.com/wiki/fmhy/FMHY/Streaming.md";

        public StreamScraperService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<StreamScraperService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task SyncAndCheckStreamsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Stream Scraper and Health Check sync...");

            try
            {
                var client = _httpClientFactory.CreateClient("StreamScraper");
                string rawMarkdown = await client.GetStringAsync(WikiSportsUrl, cancellationToken);

                string targetSection = ExtractLiveTvSportsSection(rawMarkdown);
                var parsedStreams = ParseMarkdownAndResolveMirrors(targetSection);

                _logger.LogInformation("Parsed {Count} stream links from markdown section.", parsedStreams.Count);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await UpsertStreamsAsync(dbContext, parsedStreams, cancellationToken);
                }

                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await HealthCheckStreamsAsync(dbContext, cancellationToken);
                }

                _logger.LogInformation("Completed Stream Scraper and Health Check sync successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Stream Scraper sync.");
            }
        }

        private static string ExtractLiveTvSportsSection(string fullMarkdown)
        {
            int startIdx = fullMarkdown.IndexOf("# ► Live TV / Sports", StringComparison.OrdinalIgnoreCase);
            if (startIdx == -1)
            {
                startIdx = fullMarkdown.IndexOf("Live TV / Sports", StringComparison.OrdinalIgnoreCase);
            }

            if (startIdx == -1)
            {
                return fullMarkdown;
            }

            int contentStart = startIdx;
            int nextH1 = fullMarkdown.IndexOf("\n# ► ", contentStart + 5, StringComparison.OrdinalIgnoreCase);
            if (nextH1 == -1)
            {
                nextH1 = fullMarkdown.IndexOf("\n# ", contentStart + 5, StringComparison.OrdinalIgnoreCase);
            }

            if (nextH1 != -1)
            {
                return fullMarkdown.Substring(contentStart, nextH1 - contentStart);
            }

            return fullMarkdown.Substring(contentStart);
        }

        public static List<ParsedStreamItem> ParseMarkdownAndResolveMirrors(string sectionMarkdown)
        {
            var results = new List<ParsedStreamItem>();
            string currentCategory = "Live TV / Sports";

            string[] lines = sectionMarkdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string linkPattern = @"\[([^\]]+)\]\((https?://[^\)]+)\)";

            var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Note", "adblocker", "VPN", "throwaway", "alias", "Mirrors",
                "Discord", "Telegram", "Bypass Blocks", "Status", "TG", "X"
            };

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("##"))
                {
                    string categoryTitle = Regex.Replace(trimmed, @"^#+\s*([►▷]?\s*)?", "").Trim();
                    if (!string.IsNullOrWhiteSpace(categoryTitle))
                    {
                        currentCategory = categoryTitle;
                    }
                    continue;
                }

                MatchCollection matches = Regex.Matches(trimmed, linkPattern);
                if (matches.Count == 0) continue;

                var validLineLinks = new List<(string RawName, string CleanName, string Url)>();

                foreach (Match match in matches)
                {
                    string rawName = match.Groups[1].Value.Trim();
                    string cleanName = Regex.Replace(rawName, @"[\*⭐\t\r\n]|[\u200B-\u200D\uFEFF]", "").Trim();
                    string url = match.Groups[2].Value.Trim();

                    if (url.StartsWith("#") ||
                        url.Contains("github.com/fmhy") ||
                        url.Contains("reddit.com/r/FREEMEDIAHECKYEAH") ||
                        excludedNames.Contains(cleanName))
                    {
                        continue;
                    }

                    validLineLinks.Add((rawName, cleanName, url));
                }

                if (validLineLinks.Count == 0) continue;

                string primaryName = validLineLinks.FirstOrDefault(l => !IsMirrorTag(l.CleanName)).CleanName;
                if (string.IsNullOrWhiteSpace(primaryName))
                {
                    primaryName = validLineLinks[0].CleanName;
                }

                for (int i = 0; i < validLineLinks.Count; i++)
                {
                    var (rawName, cleanName, url) = validLineLinks[i];
                    string resolvedSiteName;

                    if (i == 0 || cleanName.Equals(primaryName, StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedSiteName = primaryName;
                    }
                    else if (IsMirrorTag(cleanName))
                    {
                        resolvedSiteName = cleanName.StartsWith(".")
                            ? $"{primaryName} ({cleanName})"
                            : $"{primaryName} (Mirror {cleanName})";
                    }
                    else
                    {
                        resolvedSiteName = cleanName;
                    }

                    if (!results.Any(r => r.TargetUrl.Equals(url, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new ParsedStreamItem
                        {
                            SiteName = resolvedSiteName,
                            TargetUrl = url,
                            Category = currentCategory
                        });
                    }
                }
            }

            return results;
        }

        private static bool IsMirrorTag(string name)
        {
            if (int.TryParse(name, out _)) return true;
            if (name.StartsWith(".")) return true;
            if (name.Equals("2", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("3", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("4", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("5", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("6", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static (string StreamType, bool RequiresSearch, string? ResolverKey) DetermineProviderMetadata(string siteName, string targetUrl)
        {
            string lowerUrl = targetUrl.ToLowerInvariant();
            string lowerName = siteName.ToLowerInvariant();

            if (lowerUrl.Contains("dlhd.st") || lowerUrl.Contains("daddylive") || lowerName.Contains("daddylive"))
            {
                return ("Directory", true, "daddylive");
            }

            if (lowerUrl.Contains("streamed.pk") || lowerUrl.Contains("streamed.st") || lowerName.Contains("streamed"))
            {
                return ("Directory", true, "streamed");
            }

            if (lowerUrl.Contains("score808") || lowerName.Contains("score808"))
            {
                return ("Directory", true, "score808");
            }

            return ("Direct", false, null);
        }

        private static async Task UpsertStreamsAsync(AppDbContext dbContext, List<ParsedStreamItem> parsedStreams, CancellationToken cancellationToken)
        {
            var existingMap = await dbContext.StreamLinks
                .ToDictionaryAsync(s => s.TargetUrl, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var now = DateTime.UtcNow;

            foreach (var parsed in parsedStreams)
            {
                var (streamType, requiresSearch, resolverKey) = DetermineProviderMetadata(parsed.SiteName, parsed.TargetUrl);

                if (existingMap.TryGetValue(parsed.TargetUrl, out var existing))
                {
                    existing.SiteName = parsed.SiteName;
                    existing.Category = parsed.Category;
                    existing.StreamType = streamType;
                    existing.RequiresChannelSearch = requiresSearch;
                    existing.ChannelResolverKey = resolverKey;
                    existing.UpdatedAt = now;
                }
                else
                {
                    var newEntity = new StreamLink
                    {
                        SiteName = parsed.SiteName,
                        TargetUrl = parsed.TargetUrl,
                        Category = parsed.Category,
                        IsOnline = true,
                        StreamType = streamType,
                        RequiresChannelSearch = requiresSearch,
                        ChannelResolverKey = resolverKey,
                        LastCheckedAt = now,
                        UpdatedAt = now
                    };
                    dbContext.StreamLinks.Add(newEntity);
                    existingMap[parsed.TargetUrl] = newEntity;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task HealthCheckStreamsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            var allStreams = await dbContext.StreamLinks.ToListAsync(cancellationToken);
            if (!allStreams.Any()) return;

            var client = _httpClientFactory.CreateClient("HealthCheck");

            using var semaphore = new SemaphoreSlim(10);
            var tasks = allStreams.Select(async stream =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    bool isOnline = await PingStreamUrlAsync(client, stream.TargetUrl, cancellationToken);
                    stream.IsOnline = isOnline;
                    stream.LastCheckedAt = DateTime.UtcNow;
                }
                catch
                {
                    stream.IsOnline = false;
                    stream.LastCheckedAt = DateTime.UtcNow;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static async Task<bool> PingStreamUrlAsync(HttpClient client, string url, CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (response.IsSuccessStatusCode || (int)response.StatusCode < 400 || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return true;
                }

                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                return getResponse.IsSuccessStatusCode || (int)getResponse.StatusCode < 400 || getResponse.StatusCode == System.Net.HttpStatusCode.Forbidden;
            }
            catch
            {
                return false;
            }
        }

        public class ParsedStreamItem
        {
            public string SiteName { get; set; } = string.Empty;
            public string TargetUrl { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
        }
    }
}
