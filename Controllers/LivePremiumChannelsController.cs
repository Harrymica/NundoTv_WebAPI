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

        [HttpGet("search-by-name")]
        public async Task<ActionResult<IEnumerable<LivePremiumChannel>>> SearchByName([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "Name parameter is required." });
            }

            var channels = await _db.LivePremiumChannels
                .AsNoTracking()
                .Where(c => EF.Functions.ILike(c.Name, $"%{name}%"))
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(channels);
        }

        [HttpGet("countries")]
        public async Task<ActionResult<IEnumerable<string>>> GetCountries()
        {
            var countries = await _db.LivePremiumChannels
                .AsNoTracking()
                .Where(c => !string.IsNullOrEmpty(c.Country))
                .Select(c => c.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(countries);
        }

        [HttpPost("addfeaturedchannels")]
        public async Task<ActionResult<Featured>> AddFeaturedChannels([FromBody] Featured featured)
        {
            if(featured == null) 
            {
                return BadRequest(400);
            }
            featured.Id = Guid.Empty; 
            var result = await _db.Featured.AddAsync(featured);
            await _db.SaveChangesAsync();

            if(result == null)
            {
                return BadRequest(new { message = "Error: the featured channel was not Added"});
            }

            return Ok(featured);
            
        }

        [HttpPost("addfeaturedchannelsList")]
        public async Task<ActionResult<IEnumerable<Featured>>> AddFeaturedChannels([FromBody] List<Featured> featuredChannels)
        {
            // 1. Guard clause: Ensure the list isn't null or empty
            if (featuredChannels == null || !featuredChannels.Any()) 
            {
                return BadRequest(new { message = "Error: The channel list cannot be empty." });
            }

            // 2. Loop through and wipe the incoming IDs so EF Core auto-generates them cleanly
            foreach (var channel in featuredChannels)
            {
                channel.Id = Guid.Empty; 
            }

            // 3. Use AddRangeAsync to stage the entire list in memory
            await _db.Featured.AddRangeAsync(featuredChannels);
            
            // 4. Save changes once to commit the entire batch to the database
            await _db.SaveChangesAsync();

            // 5. Return the newly created collection containing their fresh IDs
            return Ok(featuredChannels);
        }

        [HttpGet("getfeatured")]
        public async Task<ActionResult<List<Featured>>> GetFeaturedList()
        {
            var result = await _db.Featured.AsNoTracking().ToListAsync();

            if (result == null) 
            {
                return BadRequest(new { message = "No channel(s) found" });
            }

            return Ok(result);
        }

        /*[HttpDelete("deletefeaturedchannel/{id}")]
        public async Task<ActionResult> DeleteChannel(Guid id)
        {
            var channel = await _db.Featured.FirstOrDefaultAsync(i => i.Id == id);

            // 2. Safely check if it exists before trying to delete it
            if (channel == null)
            {
                return NotFound("Channel not found.");
            }

            _db.Remove(channel);

            await _db.SaveChangesAsync();

            return NoContent();
        }*/

        [HttpDelete("deletefeaturedchannel/{id}")]
        public async Task<IActionResult> DeleteChannel(Guid id)
        {
            // This executes a direct "DELETE FROM Featured WHERE Id = id" SQL query instantly
            int rowsAffected = await _db.Featured
                .Where(i => i.Id == id)
                .ExecuteDeleteAsync();

            // If 0 rows were changed, it means that ID didn't exist
            if (rowsAffected == 0)
            {
                return NotFound("Channel not found.");
            }

            return NoContent();
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
