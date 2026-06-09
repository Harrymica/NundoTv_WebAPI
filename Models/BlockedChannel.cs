using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NundoTv_WebAPI.Models
{
    public class BlockedChannel
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

        public List<string> AltNames { get; set; } = new();

        [MaxLength(256)]
        public string? Network { get; set; }

        public List<string> Owners { get; set; } = new();

        [MaxLength(8)]
        public string? Country { get; set; }

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

        [MaxLength(1024)]
        public string? StreamUrl { get; set; }

        [MaxLength(1024)]
        public string? Logo { get; set; }

        /// <summary>
        /// Reason the channel was blocked: "nsfw", "dmca", or "keyword_match"
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string BlockReason { get; set; } = default!;

        /// <summary>
        /// Reference URL from the iptv-org blocklist (if applicable)
        /// </summary>
        [MaxLength(512)]
        public string? BlockRef { get; set; }

        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}
