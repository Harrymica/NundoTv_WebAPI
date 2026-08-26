using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NundoTv_WebAPI.Data;
using NundoTv_WebAPI.Models;

namespace NundoTv_WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbackController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FeedbackController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Message))
            {
                return BadRequest(new { message = "Feedback message cannot be empty." });
            }

            var feedback = new Feedback
            {
                Id = Guid.NewGuid().ToString(),
                UserId = dto.UserId,
                UserName = dto.UserName,
                UserEmail = dto.UserEmail,
                FeedbackType = string.IsNullOrWhiteSpace(dto.FeedbackType) ? "General Feedback" : dto.FeedbackType,
                Rating = dto.Rating < 1 ? 1 : (dto.Rating > 5 ? 5 : dto.Rating),
                Message = dto.Message.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thank you for your feedback!", feedbackId = feedback.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetFeedbacks([FromQuery] int limit = 50)
        {
            var feedbacks = await _context.Feedbacks
                .AsNoTracking()
                .OrderByDescending(f => f.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return Ok(feedbacks);
        }
    }

    public class FeedbackDto
    {
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string FeedbackType { get; set; } = "General Feedback";
        public int Rating { get; set; } = 5;
        public string Message { get; set; } = string.Empty;
    }
}
