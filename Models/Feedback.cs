using System.ComponentModel.DataAnnotations;

namespace NundoTv_WebAPI.Models
{
    public class Feedback
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string? UserId { get; set; }

        public string? UserName { get; set; }

        public string? UserEmail { get; set; }

        [Required]
        public string FeedbackType { get; set; } = "General Feedback";

        [Range(1, 5)]
        public int Rating { get; set; } = 5;

        [Required]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
