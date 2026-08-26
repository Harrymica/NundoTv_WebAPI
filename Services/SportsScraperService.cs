using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Services
{
    public class SportsScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _db;
        private readonly ILogger<SportsScraperService> _logger;
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

        public SportsScraperService(HttpClient httpClient, AppDbContext db, ILogger<SportsScraperService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
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

            // 2. Execute Scraping Source 1: Real-Time Sports Fixture & Stream API Engine
            try
            {
                await ApplyRateLimitingAsync();
                var matchesFromApi = await ScrapeLiveSoccerFixturesAsync(availableSportsStreams, ct);
                if (matchesFromApi.Count > 0)
                {
                    scrapedMatches.AddRange(matchesFromApi);
                    _logger.LogInformation("Extracted {Count} live matches from Primary Fixture Aggregator.", matchesFromApi.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Primary Sports Scraping step.");
            }

            // 3. Execute Scraping Source 2: HTML Scraping via HtmlAgilityPack (Sports Aggregator Pages)
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during HtmlAgilityPack Sports Scraping step.");
            }

            // 4. Save/Upsert into Database
            if (scrapedMatches.Count > 0)
            {
                await SaveMatchesToDatabaseAsync(scrapedMatches, ct);
            }
            else
            {
                _logger.LogWarning("No new matches extracted during scraping pipeline execution.");
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
                    request.Headers.UserAgent.ParseAdd(GetRandomUserAgent());

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

        private async Task SaveMatchesToDatabaseAsync(List<SportsMatch> matches, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Upserting {Count} scraped sports matches into database...", matches.Count);

                var existingList = await _db.SportsMatches.AsNoTracking().ToListAsync(ct);
                var existingMatches = existingList.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());

                foreach (var match in matches)
                {
                    if (existingMatches.TryGetValue(match.Id, out var existing))
                    {
                        _db.Entry(existing).State = EntityState.Modified;
                        existing.HomeTeam = match.HomeTeam;
                        existing.AwayTeam = match.AwayTeam;
                        if (!string.IsNullOrEmpty(match.HomeLogo)) existing.HomeLogo = match.HomeLogo;
                        if (!string.IsNullOrEmpty(match.AwayLogo)) existing.AwayLogo = match.AwayLogo;
                        existing.League = match.League;
                        existing.KickOffTime = match.KickOffTime;
                        existing.Status = match.Status;
                        existing.Score = match.Score;
                        if (!string.IsNullOrEmpty(match.StreamUrl)) existing.StreamUrl = match.StreamUrl;
                    }
                    else
                    {
                        await _db.SportsMatches.AddAsync(match, ct);
                    }
                }

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Successfully saved sports matches to database.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scraped sports matches to database.");
            }
        }
    }
}
