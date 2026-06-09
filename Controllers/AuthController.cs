using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Syncs a Clerk-authenticated user to the local database.
        /// Called from the mobile app after successful Clerk sign-in/sign-up.
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> SyncUser([FromBody] SyncUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ClerkId))
                return BadRequest(new { message = "ClerkId is required." });

            // Try to find existing user by ClerkId
            var user = await _context.Users
                .Include(u => u.UserInterests)
                .FirstOrDefaultAsync(u => u.ClerkId == dto.ClerkId);

            if (user == null)
            {
                // Create new user
                user = new User
                {
                    Id = Guid.NewGuid(),
                    ClerkId = dto.ClerkId,
                    Name = dto.Name ?? string.Empty,
                    Email = dto.Email ?? string.Empty,
                    Country = dto.Country ?? string.Empty,
                    ProfileImageUrl = dto.ProfileImageUrl,
                    HasCompletedOnboarding = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Update existing user profile from Clerk
                user.Name = dto.Name ?? user.Name;
                user.Email = dto.Email ?? user.Email;
                user.ProfileImageUrl = dto.ProfileImageUrl ?? user.ProfileImageUrl;
                user.Country = dto.Country ?? user.Country;

                await _context.SaveChangesAsync();
            }

            return Ok(new SyncUserResponseDto
            {
                Id = user.Id,
                ClerkId = user.ClerkId,
                Name = user.Name,
                Email = user.Email,
                Country = user.Country,
                ProfileImageUrl = user.ProfileImageUrl,
                HasCompletedOnboarding = user.HasCompletedOnboarding
            });
        }
    }

    public class SyncUserDto
    {
        public string ClerkId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class SyncUserResponseDto
    {
        public Guid Id { get; set; }
        public string ClerkId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public bool HasCompletedOnboarding { get; set; }
    }
}
