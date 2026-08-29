using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services.ChannelResolvers;

namespace NundoTv_WebAPI.Services
{
    public class SportsScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _db;
        private readonly ILogger<SportsScraperService> _logger;
        private readonly DaddyLiveResolver _daddyLiveResolver;
        private static readonly Random _random = new Random();

        // User-Agent Pool for rotation to prevent scraper blocking
        private static readonly string[] UserAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0"
        };

        public SportsScraperService(HttpClient httpClient, AppDbContext db, ILogger<SportsScraperService> logger, DaddyLiveResolver daddyLiveResolver)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
            _daddyLiveResolver = daddyLiveResolver;
        }

        private string GetRandomUserAgent()
        {
            return UserAgents[_random.Next(UserAgents.Length)];
        }

        private async Task ApplyRateLimitingAsync()
        {
            int delayMs = _random.Next(800, 1800);
            await Task.Delay(delayMs);
        }

        public async Task<List<SportsMatch>> ScrapeAndSyncMatchesAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Starting Live Sports Scraper Pipeline...");

            var scrapedMatches = new List<SportsMatch>();

            // 1. Fetch available Sports channels from DB for fallback/mapping
            var availableSportsStreams = await GetAvailableSportsChannelStreamsAsync(ct);

            // 2. Execute Scraping Source 1: DaddyLive Live Matches & Schedule Scraper
            try
            {
                var daddyLiveMatches = await ScrapeDaddyLiveMatchesAsync(ct);
                if (daddyLiveMatches.Count > 0)
                {
                    scrapedMatches.AddRange(daddyLiveMatches);
                    _logger.LogInformation("Extracted {Count} live matches from DaddyLive Schedule.", daddyLiveMatches.Count);
                    
                    // Save DaddyLive matches immediately so API endpoint can serve them right away
                    await SaveMatchesToDatabaseAsync(scrapedMatches, ct);

                    // Fast logo enrichment (limited lookups)
                    try
                    {
                        await EnrichMatchLogosAsync(daddyLiveMatches, ct);
                        await SaveMatchesToDatabaseAsync(scrapedMatches, ct);
                    }
                    catch (Exception logoEx)
                    {
                        _logger.LogWarning(logoEx, "Logo enrichment completed with partial results.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during DaddyLive Sports Scraping step.");
            }

            // 3. Execute Scraping Source 2: Real-Time Sports Fixture & Stream API Engine
            try
            {
                await ApplyRateLimitingAsync();
                var matchesFromApi = await ScrapeLiveSoccerFixturesAsync(availableSportsStreams, ct);
                if (matchesFromApi.Count > 0)
                {
                    foreach (var m in matchesFromApi)
                    {
                        if (!scrapedMatches.Any(existing => existing.HomeTeam.Equals(m.HomeTeam, StringComparison.OrdinalIgnoreCase)
                                                         && existing.AwayTeam.Equals(m.AwayTeam, StringComparison.OrdinalIgnoreCase)))
                        {
                            scrapedMatches.Add(m);
                        }
                    }
                    _logger.LogInformation("Extracted {Count} live matches from Primary Fixture Aggregator.", matchesFromApi.Count);
                    await SaveMatchesToDatabaseAsync(scrapedMatches, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Primary Sports Scraping step.");
            }

            // 4. Execute Scraping Source 3: HTML Scraping via HtmlAgilityPack (Sports Aggregator Pages)
            try
            {
                await ApplyRateLimitingAsync();
                var htmlMatches = await ScrapeAggregatorHtmlPagesAsync(availableSportsStreams, ct);
                if (htmlMatches.Count > 0)
                {
                    foreach (var m in htmlMatches)
                    {
                        if (!scrapedMatches.Any(existing => existing.HomeTeam.Equals(m.HomeTeam, StringComparison.OrdinalIgnoreCase)
                                                         && existing.AwayTeam.Equals(m.AwayTeam, StringComparison.OrdinalIgnoreCase)))
                        {
                            scrapedMatches.Add(m);
                        }
                    }
                    await SaveMatchesToDatabaseAsync(scrapedMatches, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during HtmlAgilityPack Sports Scraping step.");
            }

            return scrapedMatches;
        }

        private async Task<List<string>> GetAvailableSportsChannelStreamsAsync(CancellationToken ct)
        {
            var streams = new List<string>();

            try
            {
                var premiumSports = await _db.LivePremiumChannels
                    .AsNoTracking()
                    .Where(c => c.Name.ToLower().Contains("sport") || c.CategoriesRaw.ToLower().Contains("sport"))
                    .Select(c => c.StreamUrl)
                    .ToListAsync(ct);

                var regularSports = await _db.LiveChannels
                    .AsNoTracking()
                    .Where(c => c.Name.ToLower().Contains("sport") || c.CategoriesRaw.ToLower().Contains("sport"))
                    .Select(c => c.StreamUrl)
                    .ToListAsync(ct);

                streams.AddRange(premiumSports.Where(s => !string.IsNullOrEmpty(s)));
                streams.AddRange(regularSports.Where(s => !string.IsNullOrEmpty(s)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query DB for fallback sports channel streams.");
            }

            if (streams.Count == 0)
            {
                // Fallback live HLS streams for testing when database has no channels yet
                streams.Add("https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8");
                streams.Add("https://demo.unified-streaming.com/k8s/live/stable/sintel.ism/.m3u8");
                streams.Add("https://cph-p2p-msl.akamaized.net/hls/live/2000341/test/master.m3u8");
            }

            return streams.Distinct().ToList();
        }

        private async Task<List<SportsMatch>> ScrapeLiveSoccerFixturesAsync(List<string> sportsStreams, CancellationToken ct)
        {
            var results = new List<SportsMatch>();

            // List of active ESPN soccer league slugs
            var leagueSlugs = new[]
            {
                "all",            // All live/today soccer matches
                "eng.1",          // English Premier League
                "esp.1",          // Spanish La Liga
                "usa.1",          // MLS
                "uefa.champions", // UEFA Champions League
                "ger.1",          // German Bundesliga
                "ita.1",          // Italian Serie A
                "fra.1",          // French Ligue 1
                "mex.1",          // Mexican Liga MX
                "fifa.world"      // World Cup / International
            };

            int index = 0;
            var processedMatchIds = new HashSet<string>();

            foreach (var slug in leagueSlugs)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    string requestUrl = $"https://site.api.espn.com/apis/site/v2/sports/soccer/{slug}/scoreboard";
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36");
                    request.Headers.Add("Accept", "application/json, text/plain, */*");

                    using var response = await _httpClient.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("ESPN Scoreboard API for league {League} returned non-success code: {StatusCode}", slug, response.StatusCode);
                        continue;
                    }

                    var jsonString = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("events", out var eventsElement))
                    {
                        continue;
                    }

                    int leagueMatchCount = 0;

                    foreach (var evt in eventsElement.EnumerateArray())
                    {
                        try
                        {
                            var matchId = evt.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();

                            if (processedMatchIds.Contains(matchId))
                            {
                                continue;
                            }

                            // Extract competitions array first
                            if (!evt.TryGetProperty("competitions", out var competitions) || competitions.GetArrayLength() == 0)
                            {
                                continue;
                            }

                            var comp = competitions[0];

                            // Status is located inside competition object (comp.status), or fall back to event level
                            JsonElement statusObj;
                            if (!comp.TryGetProperty("status", out statusObj) && !evt.TryGetProperty("status", out statusObj))
                            {
                                continue;
                            }

                            var typeObj = statusObj.GetProperty("type");
                            var state = typeObj.GetProperty("state").GetString(); // "in", "pre", "post"
                            var detail = typeObj.TryGetProperty("shortDetail", out var sd) ? sd.GetString() : "LIVE";

                            string status = state == "in" ? "LIVE" : (state == "pre" ? "UPCOMING" : "FINISHED");
                            string kickOffTime = state == "in" ? $"🔴 LIVE {detail}" : (detail ?? "20:00 UTC");

                            // Extract league name from root league name or altGameNote
                            string leagueName = "Soccer League";
                            if (comp.TryGetProperty("altGameNote", out var altNote) && !string.IsNullOrEmpty(altNote.GetString()))
                            {
                                leagueName = altNote.GetString()!;
                            }
                            else if (root.TryGetProperty("leagues", out var leaguesArr) && leaguesArr.GetArrayLength() > 0 && leaguesArr[0].TryGetProperty("name", out var lNameProp))
                            {
                                leagueName = lNameProp.GetString() ?? "Soccer League";
                            }

                            if (!comp.TryGetProperty("competitors", out var competitors) || competitors.GetArrayLength() < 2)
                            {
                                continue;
                            }

                            var competitorList = competitors.EnumerateArray().ToList();
                            var homeComp = competitorList.FirstOrDefault(c => c.TryGetProperty("homeAway", out var ha) && ha.GetString() == "home");
                            var awayComp = competitorList.FirstOrDefault(c => c.TryGetProperty("homeAway", out var ha) && ha.GetString() == "away");

                            if (homeComp.ValueKind == JsonValueKind.Undefined) homeComp = competitorList[0];
                            if (awayComp.ValueKind == JsonValueKind.Undefined) awayComp = competitorList.Count > 1 ? competitorList[1] : competitorList[0];

                            string homeTeam = homeComp.GetProperty("team").GetProperty("displayName").GetString() ?? "Home Team";
                            string homeLogo = homeComp.GetProperty("team").TryGetProperty("logo", out var hLogo) ? hLogo.GetString() ?? "" : "";
                            string homeScore = homeComp.TryGetProperty("score", out var hScore) ? hScore.GetString() ?? "0" : "0";

                            string awayTeam = awayComp.GetProperty("team").GetProperty("displayName").GetString() ?? "Away Team";
                            string awayLogo = awayComp.GetProperty("team").TryGetProperty("logo", out var aLogo) ? aLogo.GetString() ?? "" : "";
                            string awayScore = awayComp.TryGetProperty("score", out var aScore) ? aScore.GetString() ?? "0" : "0";

                            string scoreStr = state == "pre" ? "vs" : $"{homeScore} - {awayScore}";

                            // Map match to stream URL from DB / fallback pool
                            string assignedStream = sportsStreams[index % sportsStreams.Count];

                            results.Add(new SportsMatch
                            {
                                Id = matchId,
                                HomeTeam = homeTeam,
                                AwayTeam = awayTeam,
                                HomeLogo = homeLogo,
                                AwayLogo = awayLogo,
                                League = leagueName,
                                KickOffTime = kickOffTime,
                                Status = status,
                                Score = scoreStr,
                                StreamUrl = assignedStream,
                                Category = "Football",
                                CreatedAt = DateTime.UtcNow
                            });

                            processedMatchIds.Add(matchId);
                            leagueMatchCount++;
                            index++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing individual match element in scraper API for league {League}.", slug);
                        }
                    }

                    if (leagueMatchCount > 0)
                    {
                        _logger.LogInformation("Parsed {Count} matches for league {League}.", leagueMatchCount, slug);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to scrape ESPN league endpoint: {League}", slug);
                }
            }

            _logger.LogInformation("Successfully parsed total of {Count} sports matches from ESPN API across all leagues.", results.Count);
            return results;
        }

        private async Task<List<SportsMatch>> ScrapeAggregatorHtmlPagesAsync(List<string> sportsStreams, CancellationToken ct)
        {
            var results = new List<SportsMatch>();
            try
            {
                // HtmlAgilityPack DOM parsing example targeting a sports schedule aggregator page
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.livesoccertv.com/");
                request.Headers.UserAgent.ParseAdd(GetRandomUserAgent());

                using var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return results;

                var htmlString = await response.Content.ReadAsStringAsync(ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlString);

                // Find match elements in HTML DOM
                var matchNodes = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'matchrow')] | //div[contains(@class, 'match-card')]");
                if (matchNodes != null)
                {
                    int idx = 0;
                    foreach (var node in matchNodes)
                    {
                        var teamsText = node.SelectSingleNode(".//span[contains(@class, 'teams')] | .//a[contains(@class, 'match')]")?.InnerText?.Trim();
                        var leagueText = node.SelectSingleNode(".//span[contains(@class, 'league')]")?.InnerText?.Trim() ?? "World Football";
                        var timeText = node.SelectSingleNode(".//span[contains(@class, 'time')]")?.InnerText?.Trim() ?? "LIVE";

                        if (!string.IsNullOrEmpty(teamsText) && teamsText.Contains("vs"))
                        {
                            var parts = teamsText.Split(new[] { "vs", "-" }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                results.Add(new SportsMatch
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    HomeTeam = parts[0].Trim(),
                                    AwayTeam = parts[1].Trim(),
                                    League = leagueText,
                                    KickOffTime = timeText,
                                    Status = timeText.Contains("LIVE") ? "LIVE" : "UPCOMING",
                                    Score = "vs",
                                    StreamUrl = sportsStreams[idx % sportsStreams.Count],
                                    Category = "Football",
                                    CreatedAt = DateTime.UtcNow
                                });
                                idx++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("HtmlAgilityPack HTML scraping note: {Message}", ex.Message);
            }

            return results;
        }

        private async Task<List<SportsMatch>> ScrapeDaddyLiveMatchesAsync(CancellationToken ct)
        {
            var results = new List<SportsMatch>();
            var scheduleDomains = new[] { "https://dlhd.so", "https://daddylive.mp", "https://dlhd.sx", "https://dlstreams.st" };
            string jsonString = "";
            string workingDomain = "https://dlhd.so";

            foreach (var domain in scheduleDomains)
            {
                string targetUrl = $"{domain}/schedule/schedule-generated.json";
                try
                {
                    _logger.LogInformation("Fetching DaddyLive schedule JSON from {Url}...", targetUrl);
                    using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                    request.Headers.UserAgent.ParseAdd(GetRandomUserAgent());
                    request.Headers.Add("Accept", "application/json, text/plain, */*");
                    request.Headers.Referrer = new Uri($"{domain}/");

                    using var response = await _httpClient.SendAsync(request, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        jsonString = await response.Content.ReadAsStringAsync(ct);
                        workingDomain = domain;
                        break;
                    }
                    _logger.LogWarning("DaddyLive schedule request to {Url} returned status: {StatusCode}", targetUrl, response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch DaddyLive schedule from {Url}", targetUrl);
                }
            }

            if (string.IsNullOrEmpty(jsonString))
            {
                return results;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                foreach (var headerProp in root.EnumerateObject())
                {
                    var categoryObj = headerProp.Value;
                    if (categoryObj.ValueKind != JsonValueKind.Object) continue;

                    foreach (var categoryProp in categoryObj.EnumerateObject())
                    {
                        string rawCategory = categoryProp.Name.Replace("</span>", "").Trim();
                        var eventsArray = categoryProp.Value;
                        if (eventsArray.ValueKind != JsonValueKind.Array) continue;

                        foreach (var evt in eventsArray.EnumerateArray())
                        {
                            try
                            {
                                string eventTitle = evt.TryGetProperty("event", out var eProp) ? eProp.GetString() ?? "" : "";
                                string timeStr = evt.TryGetProperty("time", out var tProp) ? tProp.GetString() ?? "LIVE" : "LIVE";

                                if (string.IsNullOrWhiteSpace(eventTitle)) continue;

                                string channelId = "";
                                string channelName = "";

                                if (evt.TryGetProperty("channels", out var chanArray) && chanArray.ValueKind == JsonValueKind.Array && chanArray.GetArrayLength() > 0)
                                {
                                    var firstChan = chanArray[0];
                                    channelId = firstChan.TryGetProperty("channel_id", out var idP) ? idP.GetString() ?? "" : "";
                                    channelName = firstChan.TryGetProperty("channel_name", out var nP) ? nP.GetString() ?? "" : "";
                                }
                                else if (evt.TryGetProperty("channels2", out var chanArray2) && chanArray2.ValueKind == JsonValueKind.Array && chanArray2.GetArrayLength() > 0)
                                {
                                    var firstChan = chanArray2[0];
                                    channelId = firstChan.TryGetProperty("channel_id", out var idP) ? idP.GetString() ?? "" : "";
                                    channelName = firstChan.TryGetProperty("channel_name", out var nP) ? nP.GetString() ?? "" : "";
                                }

                                if (string.IsNullOrEmpty(channelId)) continue;

                                string homeTeam = eventTitle;
                                string awayTeam = "";

                                if (eventTitle.Contains(" vs ", StringComparison.OrdinalIgnoreCase))
                                {
                                    var parts = Regex.Split(eventTitle, @"\s+vs\s+", RegexOptions.IgnoreCase);
                                    if (parts.Length >= 2)
                                    {
                                        homeTeam = parts[0].Trim();
                                        awayTeam = parts[1].Trim();
                                    }
                                }
                                else if (eventTitle.Contains(" v ", StringComparison.OrdinalIgnoreCase))
                                {
                                    var parts = Regex.Split(eventTitle, @"\s+v\s+", RegexOptions.IgnoreCase);
                                    if (parts.Length >= 2)
                                    {
                                        homeTeam = parts[0].Trim();
                                        awayTeam = parts[1].Trim();
                                    }
                                }

                                // Store the DaddyLive stream page URL — .m3u8 resolution is deferred to on-demand playback
                                string primaryStreamUrl = $"{workingDomain}/stream/stream-{channelId}.php";

                                string safeTitle = Regex.Replace(eventTitle, @"[^a-zA-Z0-9]", "-").ToLower();
                                if (safeTitle.Length > 30) safeTitle = safeTitle.Substring(0, 30);
                                string matchUniqueId = $"daddylive-{channelId}-{safeTitle}".TrimEnd('-');

                                results.Add(new SportsMatch
                                {
                                    Id = matchUniqueId,
                                    HomeTeam = homeTeam,
                                    AwayTeam = awayTeam,
                                    HomeLogo = "",
                                    AwayLogo = "",
                                    League = $"DaddyLive {rawCategory}",
                                    KickOffTime = $"🔴 LIVE {timeStr}",
                                    Status = "LIVE",
                                    Score = "vs",
                                    StreamUrl = primaryStreamUrl,
                                    Category = rawCategory.Contains("Soccer", StringComparison.OrdinalIgnoreCase) ? "Football" : rawCategory,
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error parsing DaddyLive event element.");
                            }
                        }
                    }
                }

                _logger.LogInformation("Successfully extracted {Count} live matches from DaddyLive Schedule JSON.", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while parsing DaddyLive schedule JSON.");
            }

            return results;
        }

        private async Task SaveMatchesToDatabaseAsync(List<SportsMatch> matches, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Syncing {Count} scraped sports matches into database (full replace)...", matches.Count);

                // Deduplicate incoming matches by Id
                matches = matches.GroupBy(m => m.Id).Select(g => g.First()).ToList();

                // Get all existing matches
                var existingMatches = await _db.SportsMatches.ToListAsync(ct);

                // Separate DaddyLive matches from other sources
                var freshDaddyLiveIds = matches.Where(m => m.Id.StartsWith("daddylive")).Select(m => m.Id).ToHashSet();
                var freshOtherIds = matches.Where(m => !m.Id.StartsWith("daddylive")).Select(m => m.Id).ToHashSet();

                // Remove all old DaddyLive & Score808 matches that are NOT in the new scrape
                var staleDaddyLive = existingMatches.Where(m => (m.Id.StartsWith("daddylive") || m.Id.StartsWith("score808")) && !freshDaddyLiveIds.Contains(m.Id)).ToList();
                if (staleDaddyLive.Count > 0)
                {
                    _db.SportsMatches.RemoveRange(staleDaddyLive);
                    _logger.LogInformation("Removed {Count} stale DaddyLive/Score808 matches no longer live.", staleDaddyLive.Count);
                }

                // Remove other-source matches older than 6 hours that aren't in the fresh scrape
                var cutoff = DateTime.UtcNow.AddHours(-6);
                var staleOther = existingMatches
                    .Where(m => !m.Id.StartsWith("daddylive") && !m.Id.StartsWith("score808") && m.CreatedAt < cutoff && !freshOtherIds.Contains(m.Id))
                    .ToList();
                if (staleOther.Count > 0)
                {
                    _db.SportsMatches.RemoveRange(staleOther);
                    _logger.LogInformation("Removed {Count} expired non-DaddyLive matches (older than 6h).", staleOther.Count);
                }

                // Upsert fresh matches
                var existingDict = existingMatches
                    .Where(m => !staleDaddyLive.Contains(m) && !staleOther.Contains(m))
                    .GroupBy(m => m.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var match in matches)
                {
                    if (existingDict.TryGetValue(match.Id, out var existing))
                    {
                        // Update existing match
                        existing.HomeTeam = match.HomeTeam;
                        existing.AwayTeam = match.AwayTeam;
                        if (!string.IsNullOrEmpty(match.HomeLogo)) existing.HomeLogo = match.HomeLogo;
                        if (!string.IsNullOrEmpty(match.AwayLogo)) existing.AwayLogo = match.AwayLogo;
                        existing.League = match.League;
                        existing.KickOffTime = match.KickOffTime;
                        existing.Status = match.Status;
                        existing.Score = match.Score;
                        if (!string.IsNullOrEmpty(match.StreamUrl)) existing.StreamUrl = match.StreamUrl;
                        existing.CreatedAt = DateTime.UtcNow; // Refresh timestamp
                    }
                    else
                    {
                        // Insert new match
                        match.CreatedAt = DateTime.UtcNow;
                        await _db.SportsMatches.AddAsync(match, ct);
                    }
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Successfully synced sports matches. Active: {Count}", matches.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scraped sports matches to database.");
            }
        }

        /// <summary>
        /// Enriches matches that have empty HomeLogo/AwayLogo by looking up team logos
        /// via the ESPN Teams Search API (free, public, no auth required).
        /// </summary>
        private async Task EnrichMatchLogosAsync(List<SportsMatch> matches, CancellationToken ct)
        {
            // Cache: team name -> logo URL to avoid duplicate API calls
            var logoCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int maxLookups = 20;
            int lookupCount = 0;

            foreach (var match in matches)
            {
                if (ct.IsCancellationRequested || lookupCount >= maxLookups) break;

                try
                {
                    // Only enrich if logo is missing
                    if (string.IsNullOrWhiteSpace(match.HomeLogo))
                    {
                        match.HomeLogo = await LookupTeamLogoAsync(match.HomeTeam, logoCache, ct);
                        lookupCount++;
                    }

                    if (string.IsNullOrWhiteSpace(match.AwayLogo) && lookupCount < maxLookups)
                    {
                        match.AwayLogo = await LookupTeamLogoAsync(match.AwayTeam, logoCache, ct);
                        lookupCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enrich logos for match {Home} vs {Away}", match.HomeTeam, match.AwayTeam);
                }
            }

            _logger.LogInformation("Logo enrichment complete. Cache contains {Count} team logos.", logoCache.Count);
        }

        private async Task<string> LookupTeamLogoAsync(string teamName, Dictionary<string, string> cache, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(teamName) || teamName == "Home Team" || teamName == "Away Team")
                return "";

            // Check cache first
            if (cache.TryGetValue(teamName, out var cachedLogo))
                return cachedLogo;

            try
            {
                // ESPN Teams Search API — searches across all soccer leagues
                string searchUrl = $"https://site.api.espn.com/apis/site/v2/sports/soccer/all/teams?search={Uri.EscapeDataString(teamName)}&limit=1";

                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.UserAgent.ParseAdd(GetRandomUserAgent());
                request.Headers.Add("Accept", "application/json");

                using var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    cache[teamName] = "";
                    return "";
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Navigate: root.sports[0].leagues[0].teams[0].team.logos[0].href
                if (root.TryGetProperty("sports", out var sports) && sports.GetArrayLength() > 0)
                {
                    var sport = sports[0];
                    if (sport.TryGetProperty("leagues", out var leagues) && leagues.GetArrayLength() > 0)
                    {
                        var league = leagues[0];
                        if (league.TryGetProperty("teams", out var teams) && teams.GetArrayLength() > 0)
                        {
                            var teamObj = teams[0];
                            if (teamObj.TryGetProperty("team", out var team))
                            {
                                if (team.TryGetProperty("logos", out var logos) && logos.GetArrayLength() > 0)
                                {
                                    string logoUrl = logos[0].TryGetProperty("href", out var href) ? href.GetString() ?? "" : "";
                                    cache[teamName] = logoUrl;
                                    return logoUrl;
                                }
                            }
                        }
                    }
                }

                cache[teamName] = "";
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ESPN team logo lookup failed for: {Team}", teamName);
                cache[teamName] = "";
                return "";
            }
        }
    }
}
