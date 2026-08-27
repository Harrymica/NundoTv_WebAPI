using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;
using NundoTv_WebAPI.Services;
using NundoTv_WebAPI.Services.ChannelResolvers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NundoTv_WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScrapperController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IStreamScraperService _scraperService;

        public ScrapperController(AppDbContext dbContext, IStreamScraperService scraperService)
        {
            _dbContext = dbContext;
            _scraperService = scraperService;
        }

        [HttpGet("streams")]
        public async Task<IActionResult> GetStreams(
            [FromQuery] string? category,
            [FromQuery] string? search,
            [FromQuery] bool? isOnline,
            [FromQuery] bool groupByCategory = false)
        {
            try
            {
                var query = _dbContext.StreamLinks.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(s => s.Category.ToLower().Contains(category.ToLower()));
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(s => s.SiteName.ToLower().Contains(search.ToLower()));
                }

                if (isOnline.HasValue)
                {
                    query = query.Where(s => s.IsOnline == isOnline.Value);
                }

                var streams = await query
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.SiteName)
                    .ToListAsync();

                if (groupByCategory)
                {
                    var grouped = streams
                        .GroupBy(s => s.Category)
                        .Select(g => new
                        {
                            Category = g.Key,
                            Count = g.Count(),
                            Streams = g.ToList()
                        });

                    return Ok(grouped);
                }

                return Ok(streams);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to fetch stream links.", Details = ex.Message });
            }
        }

        [HttpGet("streams/{id:int}/channels")]
        public async Task<IActionResult> GetStreamChannels(
            int id,
            [FromServices] IChannelResolverService channelResolverService,
            CancellationToken cancellationToken)
        {
            try
            {
                var streamLink = await _dbContext.StreamLinks.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
                if (streamLink == null)
                {
                    return NotFound(new { Error = $"StreamLink with ID {id} was not found." });
                }

                var channels = await channelResolverService.ResolveChannelsAsync(streamLink, cancellationToken);
                return Ok(channels);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to resolve channels for stream link.", Details = ex.Message });
            }
        }

        [HttpGet("getmarkdown")]
        public async Task<IActionResult> GetMarkdown()
        {
            try
            {
                var streams = await _dbContext.StreamLinks.AsNoTracking()
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.SiteName)
                    .ToListAsync();

                if (!streams.Any())
                {
                    await _scraperService.SyncAndCheckStreamsAsync();
                    streams = await _dbContext.StreamLinks.AsNoTracking()
                        .OrderBy(s => s.Category)
                        .ThenBy(s => s.SiteName)
                        .ToListAsync();
                }

                return Ok(streams);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to load stream links.", Details = ex.Message });
            }
        }

        [HttpPost("sync")]
        public async Task<IActionResult> TriggerSync()
        {
            try
            {
                _ = Task.Run(() => _scraperService.SyncAndCheckStreamsAsync());
                return Ok(new { Message = "Background stream scraping and health check sync initiated." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to trigger sync.", Details = ex.Message });
            }
        }

        [HttpGet("score808/matches")]
        public async Task<IActionResult> GetScore808Matches(
            [FromServices] Score808Resolver score808Resolver,
            CancellationToken cancellationToken)
        {
            try
            {
                var channels = await score808Resolver.ResolveChannelsAsync("https://a1.score808hd.tv/football", cancellationToken);
                return Ok(channels);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Failed to fetch Score808hd matches.", Details = ex.Message });
            }
        }
    }
}
