using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/live-channels")]
    public class LiveChannelsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IServiceProvider _serviceProvider;

        public LiveChannelsController(AppDbContext db, IServiceProvider serviceProvider)
        {
            _db = db;
            _serviceProvider = serviceProvider;
        }

        [HttpGet("syncchannelstoDb")]
        public async Task<IActionResult> SyncChannelsToDb(CancellationToken ct)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var syncService = scope.ServiceProvider.GetRequiredService<LiveChannelSyncService>();
                await syncService.SyncAsync(ct);
            }
            return Ok(new { message = "Synchronization completed successfully." });
        }

        private static readonly HashSet<string> PremiumKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "ESPN", "HBO", "Showtime", "Starz", "Cinemax", "Nickelodeon", "Nick Jr",
            "Cartoon Network", "Disney Channel", "Disney Junior", "Disney XD",
            "Fox Sports", "Fox News", "Fox Business", "Fox",
            "CNN", "MSNBC", "CNBC", "Bloomberg",
            "CBS", "NBC", "ABC", "PBS", "TNT", "TBS", "USA Network", "Syfy",
            "Bravo", "E!", "Oxygen", "AMC", "FX", "FXX",
            "Discovery", "Animal Planet", "TLC", "Science Channel",
            "National Geographic", "Nat Geo Wild", "History Channel", "A&E", "Lifetime",
            "Comedy Central", "MTV", "VH1", "BET", "Hallmark", "Food Network", "HGTV",
            "Paramount Network", "Peacock", "Hulu", "Netflix",
            "SuperSport", "Sky Sports", "BT Sport", "beIN Sports", "beIN",
            "Eurosport", "DAZN", "Eleven Sports", "NBA TV", "NFL Network", "MLB Network",
            "Golf Channel", "Tennis Channel", "Olympic Channel", "Star Sports",
            "Sony ESPN", "Sony Ten", "TSN", "Sportsnet", "Movistar", "Canal+", "RTL", "ZDF",
            "BBC News", "Al Jazeera", "MGM", "Warner TV"
        };

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<LiveChannel>>> GetChannels(
            [FromQuery] string? category,
            [FromQuery] string? country,
            [FromQuery] string? language,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.LiveChannels.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                var token = $"\"{category.ToLower()}\"";
                query = query.Where(c => EF.Functions.ILike(c.CategoriesRaw, $"%{token}%"));
            }

            if (!string.IsNullOrWhiteSpace(country))
            {
                query = query.Where(c => c.Country == country.ToUpper());
            }

            if (!string.IsNullOrWhiteSpace(language))
            {
                var token = $"\"{language.ToLower()}\"";
                query = query.Where(c => EF.Functions.ILike(c.LanguagesRaw, $"%{token}%"));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PaginatedResponse<LiveChannel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpGet("premium")]
        public async Task<ActionResult<PaginatedResponse<LiveChannel>>> GetPremiumChannels(
            [FromQuery] string? category,
            [FromQuery] string? country,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.LiveChannels.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                var token = $"\"{category.ToLower()}\"";
                query = query.Where(c => EF.Functions.ILike(c.CategoriesRaw, $"%{token}%"));
            }

            if (!string.IsNullOrWhiteSpace(country))
            {
                query = query.Where(c => c.Country == country.ToUpper());
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));
            }

            // Filter by premium keywords
            var keywords = PremiumKeywords.ToList();
            if (keywords.Any())
            {
                // We use a manual OR composition to ensure 100% translatability
                var firstKw = keywords[0];
                var keywordQuery = query.Where(c => EF.Functions.ILike(c.Name, $"%{firstKw}%"));
                
                for (int i = 1; i < keywords.Count; i++)
                {
                    var kw = keywords[i];
                    keywordQuery = keywordQuery.Union(query.Where(c => EF.Functions.ILike(c.Name, $"%{kw}%")));
                }
                query = keywordQuery;
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PaginatedResponse<LiveChannel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        public class PaginatedResponse<T>
        {
            public List<T> Items { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LiveChannel>> GetChannel(string id)
        {
            var channel = await _db.LiveChannels.FindAsync(id);

            if (channel == null)
            {
                return NotFound();
            }

            return Ok(channel);
        }
    }
}
