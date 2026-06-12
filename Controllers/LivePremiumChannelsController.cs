using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/live-premium-channels")]
    public class LivePremiumChannelsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IServiceProvider _serviceProvider;

        public LivePremiumChannelsController(AppDbContext db, IServiceProvider serviceProvider)
        {
            _db = db;
            _serviceProvider = serviceProvider;
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncPremiumChannels(CancellationToken ct)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var syncService = scope.ServiceProvider.GetRequiredService<LivePremiumChannelSyncService>();
                await syncService.SyncAsync(ct);
            }
            return Ok(new { message = "Premium channels synchronization completed successfully." });
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponse<LivePremiumChannel>>> GetChannels(
            [FromQuery] string? category,
            [FromQuery] string? country,
            [FromQuery] string? language,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.LivePremiumChannels.AsNoTracking().AsQueryable();

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

            return Ok(new PaginatedResponse<LivePremiumChannel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<LivePremiumChannel>> GetChannel(string id)
        {
            var channel = await _db.LivePremiumChannels.FindAsync(id);

            if (channel == null)
            {
                return NotFound();
            }

            return Ok(channel);
        }

        public class PaginatedResponse<T>
        {
            public List<T> Items { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }
    }
}
