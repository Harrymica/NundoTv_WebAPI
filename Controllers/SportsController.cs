using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class SportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SportsScraperService _scraperService;

        public SportsController(AppDbContext context, SportsScraperService scraperService)
        {
            _context = context;
            _scraperService = scraperService;
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
                    .OrderByDescending(m => m.Status == "LIVE")
                    .ThenBy(m => m.CreatedAt)
                    .ToListAsync();

                // If DB has no Score808 matches yet or list is empty, run an on-demand sync
                if (matches.Count == 0 || !matches.Any(m => m.Id.StartsWith("score808")))
                {
                    var freshlyScraped = await _scraperService.ScrapeAndSyncMatchesAsync();
                    
                    matches = await dbQuery
                        .OrderByDescending(m => m.Status == "LIVE")
                        .ThenBy(m => m.CreatedAt)
                        .ToListAsync();
                }

                // Ensure all matches have valid stream URLs
                var activeSportsChannels = await _context.LivePremiumChannels
                    .AsNoTracking()
                    .Where(c => c.Name.ToLower().Contains("sport") || c.CategoriesRaw.ToLower().Contains("sport"))
                    .Select(c => c.StreamUrl)
                    .ToListAsync();

                if (activeSportsChannels.Count == 0)
                {
                    activeSportsChannels = await _context.LiveChannels
                        .AsNoTracking()
                        .Where(c => c.Name.ToLower().Contains("sport") || c.CategoriesRaw.ToLower().Contains("sport"))
                        .Select(c => c.StreamUrl)
                        .ToListAsync();
                }

                string defaultStream = activeSportsChannels.FirstOrDefault() ?? "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";

                int idx = 0;
                foreach (var match in matches)
                {
                    if (string.IsNullOrEmpty(match.StreamUrl))
                    {
                        match.StreamUrl = activeSportsChannels.Count > 0
                            ? activeSportsChannels[idx % activeSportsChannels.Count]
                            : defaultStream;
                    }
                    idx++;
                }

                return Ok(matches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred fetching live sports matches.", details = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/sports/scrape
        /// Triggers an immediate scrape execution
        /// </summary>
        [HttpPost("sports/scrape")]
        public async Task<IActionResult> TriggerScrape()
        {
            try
            {
                var matches = await _scraperService.ScrapeAndSyncMatchesAsync();
                
                return Ok(new { message = "Live sports match scrape completed successfully.", count = matches.Count, matches });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Scrape failed", details = ex.Message });
            }
        }
    }
}
