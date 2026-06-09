using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NundoTv_WebAPI.Models
{
    public class Channel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The iptv-org unique identifier, e.g. "AnhuiTV.cn"
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string IptvId { get; set; } = default!;

        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = default!;

        /// <summary>
        /// Alternative names stored as JSON array, e.g. ["安徽卫视"]
        /// </summary>
        public List<string> AltNames { get; set; } = new();

        [MaxLength(256)]
        public string? Network { get; set; }

        /// <summary>
        /// Channel owners stored as JSON array
        /// </summary>
        public List<string> Owners { get; set; } = new();

        /// <summary>
        /// ISO 3166-1 alpha-2 country code
        /// </summary>
        [MaxLength(8)]
        public string? Country { get; set; }

        /// <summary>
        /// Categories stored as JSON array, e.g. ["general", "news"]
        /// </summary>
        public List<string> Categories { get; set; } = new();

        public bool IsNsfw { get; set; }

        [MaxLength(16)]
        public string? Launched { get; set; }

        [MaxLength(16)]
        public string? Closed { get; set; }

        [MaxLength(128)]
        public string? ReplacedBy { get; set; }

        [MaxLength(512)]
        public string? Website { get; set; }

        /// <summary>
        /// Stream URL populated from streams.json
        /// </summary>
        [MaxLength(1024)]
        public string? StreamUrl { get; set; }

        /// <summary>
        /// Logo URL populated from logos endpoint or channel data
        /// </summary>
        [MaxLength(1024)]
        public string? Logo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
