using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;

namespace NundoTv_WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class iMerlController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ImerlSyncService _syncService;
        private readonly ILogger<iMerlController> _logger;

        public iMerlController(AppDbContext db, ImerlSyncService syncService, ILogger<iMerlController> logger)
        {
            _db = db;
            _syncService = syncService;
            _logger = logger;
        }

        /// <summary>
        /// Get all iMerl channels with pagination. 
        /// Supports filtering by country, category, and search.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResponse<IMerlChannel>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChannels(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? country = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null,
            [FromQuery] string? letter = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _db.ImerlChannels.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(c => c.Category != null && EF.Functions.ILike(c.Category, $"%{category}%"));

            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(c => c.Country != null && EF.Functions.ILike(c.Country, $"%{country}%"));

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => EF.Functions.ILike(c.Name, $"%{search}%"));

            if (!string.IsNullOrWhiteSpace(letter))
            {
                if (letter == "#")
                {
                    query = query.Where(c => c.Name != null && (
                        c.Name.StartsWith("0") || c.Name.StartsWith("1") || c.Name.StartsWith("2") ||
                        c.Name.StartsWith("3") || c.Name.StartsWith("4") || c.Name.StartsWith("5") ||
                        c.Name.StartsWith("6") || c.Name.StartsWith("7") || c.Name.StartsWith("8") ||
                        c.Name.StartsWith("9")
                    ));
                }
                else
                {
                    query = query.Where(c => c.Name != null && EF.Functions.ILike(c.Name, $"{letter}%"));
                }
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PaginatedResponse<IMerlChannel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Get a single iMerl channel by its database ID.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(IMerlChannel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChannel(int id)
        {
            var channel = await _db.ImerlChannels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (channel is null)
                return NotFound(new { error = $"iMerl Channel with ID {id} not found" });

            return Ok(channel);
        }

        /// <summary>
        /// Get all distinct countries alphabetically. (DB stores country in Category)
        /// </summary>
        [HttpGet("countries")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCountries()
        {
            var countries = await _db.ImerlChannels
                .Where(c => !string.IsNullOrWhiteSpace(c.Category))
                .Select(c => c.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(countries);
        }

        /// <summary>
        /// Triggers a full sync from the iMerl Free-IPTV API (M3U8).
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync(CancellationToken ct)
        {
            try
            {
                var result = await _syncService.SyncAsync(ct);
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch data from iMerl playlist");
                return StatusCode(502, new { error = "Failed to fetch data from iMerl playlist", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed");
                return StatusCode(500, new { error = "Sync failed", details = ex.Message });
            }
        }
    }
}
