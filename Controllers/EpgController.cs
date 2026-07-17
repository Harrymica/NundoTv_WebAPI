using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EpgController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EpgController> _logger;

        public EpgController(AppDbContext context, ILogger<EpgController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("now")]
        public async Task<IActionResult> GetNowPlaying([FromQuery] string? channelId = null, [FromQuery] string? channelName = null)
        {
            var now = DateTime.UtcNow;
            
            var query = _context.EpgPrograms
                .AsNoTracking()
                .Where(p => p.Start <= now && p.Stop > now);

            if (!string.IsNullOrEmpty(channelId))
            {
                var mappedId = await _context.EpgChannelMappings
                    .Where(m => m.ChannelId == channelId)
                    .Select(m => m.EpgChannelId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(mappedId))
                {
                    channelId = mappedId;
                }

                query = query.Where(p => p.ChannelId == channelId || p.ChannelId.ToLower() == channelId.ToLower());
            }

            if (!string.IsNullOrEmpty(channelName))
            {
                var term = $"%{channelName}%";
                var normalizedTerm = $"%{Regex.Replace(channelName, @"[\s\-_]+", "")}%";
                query = query.Where(p => 
                    EF.Functions.ILike(p.ChannelName!, term) || 
                    EF.Functions.ILike(p.ChannelId, term) ||
                    EF.Functions.ILike(
                        p.ChannelName!.Replace(" ", "").Replace("-", "").Replace("_", ""), 
                        normalizedTerm));
            }

            var programs = await query.ToListAsync();
            return Ok(programs);
        }

        [HttpGet("channel/{identifier}")]
        public async Task<IActionResult> GetChannelSchedule(string identifier, [FromQuery] DateTime? date = null)
        {
            var targetDate = DateTime.SpecifyKind(date?.Date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var nextDate = targetDate.AddDays(1);

            var mappedId = await _context.EpgChannelMappings
                .Where(m => m.ChannelId == identifier)
                .Select(m => m.EpgChannelId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(mappedId))
            {
                identifier = mappedId;
            }

            var term = $"%{identifier}%";
            var normalizedTerm = $"%{Regex.Replace(identifier, @"[\s\-_]+", "")}%";

            var programs = await _context.EpgPrograms
                .AsNoTracking()
                .Where(p => (p.ChannelId == identifier || 
                             EF.Functions.ILike(p.ChannelName!, term) || 
                             EF.Functions.ILike(p.ChannelId, term) ||
                             EF.Functions.ILike(
                                 p.ChannelName!.Replace(" ", "").Replace("-", "").Replace("_", ""), 
                                 normalizedTerm)) 
                         && p.Start >= targetDate && p.Start < nextDate)
                .OrderBy(p => p.Start)
                .ToListAsync();

            return Ok(programs);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllEpg([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] DateTime? date = null)
        {
            var query = _context.EpgPrograms.AsNoTracking();

            if (date.HasValue)
            {
                var targetDate = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
                var nextDate = targetDate.AddDays(1);
                query = query.Where(p => p.Start >= targetDate && p.Start < nextDate);
            }
            else
            {
                var now = DateTime.UtcNow;
                query = query.Where(p => p.Start <= now && p.Stop > now);
            }

            query = query.OrderBy(p => p.ChannelName).ThenBy(p => p.Start);

            var totalCount = await query.CountAsync();
            var programs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Data = programs
            });
        }

        [HttpGet("mappings")]
        public async Task<IActionResult> GetMappings()
        {
            var mappings = await _context.EpgChannelMappings.AsNoTracking().ToListAsync();
            return Ok(mappings);
        }

        [HttpPost("set-mapping")]
        public async Task<IActionResult> SetMapping([FromBody] MappedChannelDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ChannelId) || string.IsNullOrWhiteSpace(dto.EpgChannelId))
            {
                return BadRequest("Invalid mapping parameters.");
            }

            var existing = await _context.EpgChannelMappings
                .FirstOrDefaultAsync(m => m.EpgChannelId == dto.EpgChannelId || m.ChannelId == dto.ChannelId);

            if (existing != null)
            {
                _context.EpgChannelMappings.RemoveRange(
                    _context.EpgChannelMappings.Where(m => m.EpgChannelId == dto.EpgChannelId || m.ChannelId == dto.ChannelId)
                );
                await _context.SaveChangesAsync();
            }

            var mapping = new EpgChannelMapping
            {
                ChannelId = dto.ChannelId,
                EpgChannelId = dto.EpgChannelId
            };

            await _context.EpgChannelMappings.AddAsync(mapping);
            await _context.SaveChangesAsync();

            return Ok(mapping);
        }

        [HttpPost("map-channels")]
        public async Task<IActionResult> MapChannels()
        {
            var epgChannels = await _context.EpgPrograms
                .AsNoTracking()
                .GroupBy(p => p.ChannelId)
                .Select(g => new { ChannelId = g.Key, ChannelName = g.Max(p => p.ChannelName) })
                .ToListAsync();

            var liveChannels = await _context.LiveChannels.AsNoTracking().ToListAsync();
            var premiumChannels = await _context.LivePremiumChannels.AsNoTracking().ToListAsync();

            var mappings = new List<EpgChannelMapping>();

            string Normalize(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return "";
                name = name.ToLowerInvariant();

                if (name == "24 horas" || name == "canal 24 horas" || name == "tve 24h" || name == "24h tve")
                    return "24h";

                name = name.Replace("channel", "")
                           .Replace("tv", "")
                           .Replace("hd", "")
                           .Replace("fhd", "")
                           .Replace("sd", "")
                           .Replace("es", "")
                           .Replace("spain", "");

                return Regex.Replace(name, @"[^a-z0-9]", "");
            }

            var epgLookup = epgChannels
                .Where(e => !string.IsNullOrEmpty(e.ChannelName))
                .GroupBy(e => Normalize(e.ChannelName!))
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            void MatchAndAdd(string liveId, string liveName)
            {
                var normalizedLive = Normalize(liveName);
                if (string.IsNullOrEmpty(normalizedLive)) return;

                if (epgLookup.TryGetValue(normalizedLive, out var matchedEpg))
                {
                    if (!mappings.Any(m => m.EpgChannelId == matchedEpg.ChannelId || m.ChannelId == liveId))
                    {
                        mappings.Add(new EpgChannelMapping
                        {
                            ChannelId = liveId,
                            EpgChannelId = matchedEpg.ChannelId
                        });
                    }
                }
            }

            foreach (var ch in liveChannels)
            {
                MatchAndAdd(ch.Id, ch.Name);
            }

            foreach (var ch in premiumChannels)
            {
                MatchAndAdd(ch.Id, ch.Name);
            }

            if (mappings.Any())
            {
                var mappedEpgIds = mappings.Select(m => m.EpgChannelId).ToList();
                var mappedChannelIds = mappings.Select(m => m.ChannelId).ToList();
                
                await _context.EpgChannelMappings
                    .Where(m => mappedEpgIds.Contains(m.EpgChannelId) || mappedChannelIds.Contains(m.ChannelId))
                    .ExecuteDeleteAsync();

                await _context.EpgChannelMappings.AddRangeAsync(mappings);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = $"Successfully mapped {mappings.Count} channels.", mappingsMapped = mappings.Count });
        }

        public class MappedChannelDto
        {
            public string ChannelId { get; set; } = string.Empty;
            public string EpgChannelId { get; set; } = string.Empty;
        }
    }
}
