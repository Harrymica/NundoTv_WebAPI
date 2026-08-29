using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;
using NundoTv_WebAPI.Services.ChannelResolvers;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class SportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SportsScraperService _scraperService;
        private readonly DaddyLiveResolver _daddyLiveResolver;

        public SportsController(AppDbContext context, SportsScraperService scraperService, DaddyLiveResolver daddyLiveResolver)
        {
            _context = context;
            _scraperService = scraperService;
            _daddyLiveResolver = daddyLiveResolver;
        }

        /// <summary>
        /// GET /api/matches/live or GET /api/sports/matches
        /// Returns active live matches from the database populated by the background scraper worker.
        /// </summary>
        [HttpGet("matches/live")]
        [HttpGet("sports/matches")]
        public async Task<IActionResult> GetLiveMatches([FromQuery] string? category = null)
        {
            try
            {
                // Query database for scraped matches
                var dbQuery = _context.SportsMatches.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(category))
                {
                    dbQuery = dbQuery.Where(m => m.Category.ToLower() == category.ToLower());
                }

                var matches = await dbQuery
                    .OrderByDescending(m => m.Id.StartsWith("daddylive"))
                    .ThenByDescending(m => m.Status == "LIVE")
                    .ThenByDescending(m => m.CreatedAt)
                    .ToListAsync();

                // If DB has no DaddyLive matches yet or list is empty, trigger a background sync (non-blocking)
                if (matches.Count == 0 || !matches.Any(m => m.Id.StartsWith("daddylive")))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _scraperService.ScrapeAndSyncMatchesAsync();
                        }
                        catch { }
                    });
                }

                // Ensure all matches have valid stream URLs
                string defaultStream = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";

                foreach (var match in matches)
                {
                    if (string.IsNullOrEmpty(match.StreamUrl))
                    {
                        match.StreamUrl = defaultStream;
                    }
                }

                return Ok(matches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred fetching live sports matches.", details = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/sports/sync or /api/matches/sync
        /// Triggers an immediate scrape and database sync of live sports matches from DaddyLive.
        /// </summary>
        [HttpPost("sports/sync")]
        [HttpPost("matches/sync")]
        public async Task<IActionResult> TriggerSportsSync()
        {
            try
            {
                var matches = await _scraperService.ScrapeAndSyncMatchesAsync();
                return Ok(new { success = true, count = matches.Count, message = "Live sports match scrape completed.", matches });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Sports sync failed", details = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/sports/resolve?url=... or ?channelId=...
        /// Resolves a DaddyLive stream page URL or channel ID to a direct ad-free .m3u8 stream URL.
        /// </summary>
        [HttpGet("sports/resolve")]
        [HttpGet("matches/resolve")]
        public async Task<IActionResult> ResolveStream([FromQuery] string? url = null, [FromQuery] string? channelId = null)
        {
            string target = url ?? channelId ?? "";
            if (string.IsNullOrWhiteSpace(target))
            {
                return BadRequest(new { error = "Either url or channelId parameter is required." });
            }

            try
            {
                var directM3u8 = await _daddyLiveResolver.ResolveDirectM3u8Async(target);
                if (!string.IsNullOrEmpty(directM3u8))
                {
                    return Ok(new { success = true, streamUrl = directM3u8 });
                }

                return Ok(new { success = false, message = "Could not resolve direct .m3u8 stream", originalUrl = target });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Stream resolution failed", details = ex.Message });
            }
        }
    }
}
