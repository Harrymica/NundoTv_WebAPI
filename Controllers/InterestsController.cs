using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InterestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InterestsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns all available interests for the onboarding selection screen.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var interests = await _context.Set<NundoTv_WebAPI.Models.Interest>()
                .Select(i => new { i.Id, i.Name })
                .OrderBy(i => i.Name)
                .ToListAsync();

            return Ok(interests);
        }
    }
}
