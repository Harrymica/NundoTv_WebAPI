using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChannelsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IptvSyncService _syncService;
        private readonly ILogger<ChannelsController> _logger;

        public ChannelsController(AppDbContext db, IptvSyncService syncService, ILogger<ChannelsController> logger)
        {
            _db = db;
            _syncService = syncService;
            _logger = logger;
        }

        // ─── Premium channel keywords (~200 well-known networks/brands) ───
        private static readonly HashSet<string> PremiumKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            // US Major Networks
            "ESPN", "HBO", "Showtime", "Starz", "Cinemax", "Nickelodeon", "Nick Jr",
            "Cartoon Network", "Disney Channel", "Disney Junior", "Disney XD",
            "Fox Sports", "Fox News", "Fox Business", "Fox",
            "CNN", "MSNBC", "CNBC", "Bloomberg",
            "CBS", "NBC", "ABC", "PBS",
            "TNT", "TBS", "USA Network", "Syfy",
            "Bravo", "E!", "Oxygen",
            "AMC", "FX", "FXX",
            "Discovery", "Discovery Channel", "Animal Planet", "TLC", "Science Channel",
            "National Geographic", "Nat Geo Wild",
            "History Channel", "History", "A&E", "Lifetime",
            "Comedy Central", "MTV", "VH1", "BET",
            "Hallmark", "Hallmark Movies",
            "Food Network", "HGTV", "Travel Channel", "DIY Network",
            "Paramount Network", "Paramount",
            "Peacock", "Hulu", "Netflix",

            // Sports
            "SuperSport", "Sky Sports", "BT Sport", "beIN Sports", "beIN",
            "Eurosport", "DAZN", "Eleven Sports",
            "NBA TV", "NFL Network", "MLB Network", "NHL Network",
            "Golf Channel", "Tennis Channel", "Olympic Channel",
            "CBS Sports", "NBC Sports", "ESPN2", "ESPN3", "ESPNU", "ESPNews",
            "SEC Network", "Big Ten Network", "ACC Network", "Pac-12 Network",
            "Stadium", "Willow Cricket", "Star Sports",
            "Sony ESPN", "Sony Ten", "Sony Six",
            "TSN", "Sportsnet", "RDS",
            "Fox Deportes", "ESPN Deportes",
            "Ziggo Sport", "Polsat Sport", "Canal+ Sport",
            "Movistar Deportes",

            // UK & European
            "BBC", "BBC One", "BBC Two", "BBC Three", "BBC Four", "BBC News",
            "ITV", "ITV2", "ITV3", "ITV4",
            "Channel 4", "Channel 5", "Sky", "Sky One", "Sky Atlantic",
            "Sky Cinema", "Sky News", "Sky Arts",
            "Virgin Media", "Dave", "Gold",
            "Canal+", "Canal Plus", "TF1", "France 2", "France 3",
            "M6", "Arte",
            "RTL", "ZDF", "ARD", "ProSieben", "Sat.1", "Vox",
            "RAI", "Mediaset", "Sky Italia",
            "Antena 3", "Telecinco", "La Sexta", "Movistar",
            "RTP", "SIC", "TVI",
            "TV2", "NRK", "SVT", "DR",
            "Polsat", "TVP", "TVN",

            // Latin America
            "Televisa", "TV Azteca", "Caracol", "RCN",
            "Globo", "SBT", "Record", "Band",
            "TeleSUR", "Univision", "Telemundo",
            "Fox Sports Latin America",
            "ESPN Latin America",
            "Star+",

            // Middle East & Africa
            "MBC", "Al Jazeera", "Al Arabiya",
            "OSN", "Rotana", "Abu Dhabi TV",
            "SABC", "DStv", "StarTimes",
            "Showmax", "GOtv",
            "Nile TV",

            // Asia-Pacific
            "Star TV", "Star Plus", "Star World",
            "Zee TV", "Zee Cinema", "Zee News",
            "Colors", "Sony TV", "Sony SAB",
            "Sun TV", "Gemini TV", "Maa TV",
            "NDTV", "India Today", "Republic TV",
            "ABS-CBN", "GMA", "TV5",
            "NHK", "Fuji TV", "TBS Japan", "TV Asahi",
            "KBS", "SBS", "MBC Korea", "JTBC", "tvN",
            "CCTV", "Phoenix TV", "Dragon TV",
            "TVB", "ViuTV", "Now TV",
            "Astro", "TV3 Malaysia",
            "RCTI", "SCTV", "Indosiar", "Trans TV", "Trans7",
            "Channel 7 Thailand", "Channel 3 Thailand", "Thai PBS",
            "VTV", "HTV",
            "ABC Australia", "SBS Australia", "Foxtel", "Sky New Zealand",

            // Premium Movie / Entertainment Brands
            "Lionsgate", "MGM", "Turner Classic Movies", "TCM",
            "Epix", "IFC", "Sundance TV", "Criterion",
            "Crunchyroll", "Funimation", "Adult Swim",
            "Boomerang", "Nicktoons", "TeenNick", "Nick Music",
            "Disney+", "Freeform",
            "Curiosity Stream", "Smithsonian Channel",
            "Magnolia Network", "Cooking Channel",
            "Investigation Discovery", "ID", "Crime + Investigation",
            "Reelz", "Pop TV", "WE tv", "TV Land",
            "truTV", "Destination America",
            "AXN", "Warner TV", "Sony AXN",

            // News & Business
            "Sky News Australia", "France 24", "DW", "RT",
            "Euronews", "CGTN", "NHK World",
            "CNA", "TRT World", "Arirang",
            "i24NEWS"
        };

        /// <summary>
        /// Triggers a full sync from the iptv-org API.
        /// Fetches all channels, filters out blocked ones, and populates the database.
        /// </summary>
        [HttpPost("sync")]
        [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Sync(CancellationToken ct)
        {
            try
            {
                var result = await _syncService.SyncAsync(ct);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch data from iptv-org API");
                return StatusCode(502, new { error = "Failed to fetch data from iptv-org API", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed");
                return StatusCode(500, new { error = "Sync failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Get all clean (non-blocked) channels with pagination.
        /// Channels with a stream URL are sorted first. Logos are populated from ChannelLogos.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResponse<Channel>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChannels(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? country = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Channel> query;
            if (!string.IsNullOrWhiteSpace(category))
            {
                // Use raw SQL for category filter to bypass ValueConverter limitation
                var pattern = $"%\"{category.ToLower()}\"%";
                query = _db.Channels.FromSqlInterpolated(
                    $"SELECT * FROM \"Channels\" WHERE \"Categories\"::text ILIKE {pattern}"
                ).AsNoTracking();
                //query = _db.Channels.FromSqlInterpolated($"SELECT * FROM \"Channels\" WHERE \"Categories\"::text ILIKE {pattern}").Where(s => s.StreamUrl != null).AsNoTracking();
            }
            else
            {
                query = _db.Channels.AsNoTracking().AsQueryable();
            }

            // Only include channels that have a stream URL
            query = query.Where(c => c.StreamUrl != null && c.StreamUrl != "");

            // Filter by country
            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(c => c.Country == country.ToUpper());

            // Search by name
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));

            var totalCount = await query.CountAsync();

            // Sort alphabetically
            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Populate logos from ChannelLogos for items missing a logo
            await PopulateChannelLogosAsync(items);

            return Ok(new PaginatedResponse<Channel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Get a single channel by its database ID.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(Channel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChannel(int id)
        {
            var channel = await _db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (channel is null)
                return NotFound(new { error = $"Channel with ID {id} not found" });

            // Populate logo if missing
            if (channel.Logo == null)
            {
                var logo = await _db.ChannelLogos
                    .Where(l => l.ChannelId == channel.Id)
                    .Select(l => l.Url)
                    .FirstOrDefaultAsync();
                if (logo != null)
                    channel.Logo = logo;
            }

            return Ok(channel);
        }

        /// <summary>
        /// Get the total number of channels that have a valid stream URL.
        /// </summary>
        [HttpGet("count-with-stream")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChannelsWithStreamCount()
        {
            var count = await _db.Channels
                .AsNoTracking()
                .CountAsync(c => c.StreamUrl != null && c.StreamUrl != "");

            return Ok(new { totalWithStream = count });
        }

        /// <summary>
        /// Get all blocked channels with pagination. Supports filtering by reason, country, and category.
        /// Logos are populated from BlockedChannelLogos.
        /// </summary>
        [HttpGet("blocked")]
        [ProducesResponseType(typeof(PaginatedResponse<BlockedChannel>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBlockedChannels(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? reason = null,
            [FromQuery] string? country = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            IQueryable<BlockedChannel> query;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var pattern = $"%\"{category.ToLower()}\"%";
                query = _db.BlockedChannels.FromSqlInterpolated(
                    $"SELECT * FROM \"BlockedChannels\" WHERE \"Categories\"::text ILIKE {pattern}"
                ).AsNoTracking();
            }
            else
            {
                query = _db.BlockedChannels.AsNoTracking().AsQueryable();
            }

            if (!string.IsNullOrWhiteSpace(reason))
                query = query.Where(c => c.BlockReason == reason.ToLower());

            // Filter by country
            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(c => c.Country == country.ToUpper());

            // Search by name
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Populate logos from BlockedChannelLogos for items missing a logo
            await PopulateBlockedChannelLogosAsync(items);

            return Ok(new PaginatedResponse<BlockedChannel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Get premium channels from blocked channels (well-known networks like ESPN, HBO, Nickelodeon, etc.).
        /// Supports pagination and filtering by country, category, and search.
        /// Logos are populated from BlockedChannelLogos.
        /// </summary>
        [HttpGet("premium")]
        [ProducesResponseType(typeof(PaginatedResponse<BlockedChannel>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPremiumChannels(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? country = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            // Start from blocked channels
            IQueryable<BlockedChannel> query;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var pattern = $"%\"{category.ToLower()}\"%";
                query = _db.BlockedChannels.FromSqlInterpolated(
                    $"SELECT * FROM \"BlockedChannels\" WHERE \"Categories\"::text ILIKE {pattern}"
                ).AsNoTracking();
            }
            else
            {
                query = _db.BlockedChannels.AsNoTracking().AsQueryable();
            }

            // Filter by country
            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(c => c.Country == country.ToUpper());

            // Search by name
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));

            // Filter to only premium channels: name must contain at least one premium keyword.
            // We build a server-side OR filter using ILike for each keyword.
            var keywordList = PremiumKeywords.ToList();
            query = query.Where(c => keywordList.Any(kw => EF.Functions.ILike(c.Name, "%" + kw + "%")));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Populate logos from BlockedChannelLogos
            await PopulateBlockedChannelLogosAsync(items);

            return Ok(new PaginatedResponse<BlockedChannel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Triggers a logo sync from the iptv-org API.
        /// Fetches all logos and populates ChannelLogos / BlockedChannelLogos tables.
        /// Must be called after the main channel sync.
        /// </summary>
        [HttpPost("sync-logos")]
        [ProducesResponseType(typeof(LogoSyncResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> SyncLogos(CancellationToken ct)
        {
            try
            {
                var result = await _syncService.SyncLogosAsync(ct);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch logo data from iptv-org API");
                return StatusCode(502, new { error = "Failed to fetch logo data from iptv-org API", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logo sync failed");
                return StatusCode(500, new { error = "Logo sync failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Get sync statistics (total channels, clean, blocked, logos).
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalClean = await _db.Channels.CountAsync();
            var totalBlocked = await _db.BlockedChannels.CountAsync();
            var blockedByReason = await _db.BlockedChannels
                .GroupBy(c => c.BlockReason)
                .Select(g => new { Reason = g.Key, Count = g.Count() })
                .ToListAsync();

            var withStreams = await _db.Channels.CountAsync(c => c.StreamUrl != null);
            var withoutStreams = await _db.Channels.CountAsync(c => c.StreamUrl == null);
            var channelLogos = await _db.ChannelLogos.CountAsync();
            var blockedLogos = await _db.BlockedChannelLogos.CountAsync();

            return Ok(new
            {
                TotalClean = totalClean,
                TotalBlocked = totalBlocked,
                Total = totalClean + totalBlocked,
                WithStreams = withStreams,
                WithoutStreams = withoutStreams,
                ChannelLogos = channelLogos,
                BlockedChannelLogos = blockedLogos,
                BlockedByReason = blockedByReason
            });
        }

        // ─── Private helpers ─────────────────────────────────────────────

        /// <summary>
        /// Batch-populates the Logo field for channels whose Logo is null
        /// by looking up the first matching ChannelLogo URL.
        /// </summary>
        private async Task PopulateChannelLogosAsync(List<Channel> channels)
        {
            var channelIds = channels.Where(c => c.Logo == null).Select(c => c.Id).ToList();
            if (channelIds.Count == 0) return;

            var logoMap = await _db.ChannelLogos
                .AsNoTracking()
                .Where(l => channelIds.Contains(l.ChannelId))
                .GroupBy(l => l.ChannelId)
                .Select(g => new { ChannelId = g.Key, Url = g.First().Url })
                .ToDictionaryAsync(x => x.ChannelId, x => x.Url);

            foreach (var ch in channels)
            {
                if (ch.Logo == null && logoMap.TryGetValue(ch.Id, out var url))
                    ch.Logo = url;
            }
        }

        /// <summary>
        /// Batch-populates the Logo field for blocked channels whose Logo is null
        /// by looking up the first matching BlockedChannelLogo URL.
        /// </summary>
        private async Task PopulateBlockedChannelLogosAsync(List<BlockedChannel> channels)
        {
            var channelIds = channels.Where(c => c.Logo == null).Select(c => c.Id).ToList();
            if (channelIds.Count == 0) return;

            var logoMap = await _db.BlockedChannelLogos
                .AsNoTracking()
                .Where(l => channelIds.Contains(l.BlockedChannelId))
                .GroupBy(l => l.BlockedChannelId)
                .Select(g => new { BlockedChannelId = g.Key, Url = g.First().Url })
                .ToDictionaryAsync(x => x.BlockedChannelId, x => x.Url);

            foreach (var ch in channels)
            {
                if (ch.Logo == null && logoMap.TryGetValue(ch.Id, out var url))
                    ch.Logo = url;
            }
        }
    }

    /// <summary>
    /// Generic paginated response wrapper.
    /// </summary>
    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
