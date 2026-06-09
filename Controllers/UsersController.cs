using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPut("{userId}/update-consent")]
        public async Task<IActionResult> UpdateConsent(Guid userId, [FromBody] ConsentDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.HasConsentedToAds = dto.AdConsent;
            user.HasConsentedToDataSharing = dto.DataConsent;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Consent preferences updated." });
        }

        /// <summary>
        /// Get the interests selected by a specific user.
        /// </summary>
        [HttpGet("{userId}/interests")]
        public async Task<IActionResult> GetUserInterests(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.UserInterests)
                    .ThenInclude(ui => ui.Interest)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            var interests = user.UserInterests.Select(ui => new
            {
                ui.Interest.Id,
                ui.Interest.Name
            });

            return Ok(interests);
        }

        /// <summary>
        /// Save the user's selected interests (replaces existing selections).
        /// Sets HasCompletedOnboarding to true.
        /// </summary>
        [HttpPost("{userId}/interests")]
        public async Task<IActionResult> SaveUserInterests(Guid userId, [FromBody] SaveInterestsDto dto)
        {
            var user = await _context.Users
                .Include(u => u.UserInterests)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            // Remove existing interests
            _context.Set<UserInterest>().RemoveRange(user.UserInterests);

            // Add new interests
            foreach (var interestId in dto.InterestIds)
            {
                user.UserInterests.Add(new UserInterest
                {
                    UserId = userId,
                    InterestId = interestId
                });
            }

            user.HasCompletedOnboarding = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Interests saved successfully." });
        }
    }

    public class ConsentDto
    {
        public bool AdConsent { get; set; }
        public bool DataConsent { get; set; }
    }

    public class SaveInterestsDto
    {
        public List<Guid> InterestIds { get; set; } = new();
    }
}
