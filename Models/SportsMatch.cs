using System.ComponentModel.DataAnnotations;

namespace NundoTv_WebAPI.Models
{
    public class SportsMatch
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string HomeTeam { get; set; } = string.Empty;

        [Required]
        public string AwayTeam { get; set; } = string.Empty;

        public string? HomeLogo { get; set; }

        public string? AwayLogo { get; set; }

        public string League { get; set; } = "Premier League";

        public string KickOffTime { get; set; } = "20:00 UTC";

        public string Status { get; set; } = "LIVE";

        public string? Score { get; set; } = "0 - 0";

        public string StreamUrl { get; set; } = string.Empty;

        public string? ChannelId { get; set; }

        public string Category { get; set; } = "Football";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
