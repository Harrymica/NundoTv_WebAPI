using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NundoTv_WebAPI.Models
{
    /// <summary>
    /// Logo metadata for a blocked channel, sourced from iptv-org logos.json.
    /// </summary>
    public class BlockedChannelLogo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the BlockedChannel table.
        /// </summary>
        public int BlockedChannelId { get; set; }

        [ForeignKey(nameof(BlockedChannelId))]
        public BlockedChannel BlockedChannel { get; set; } = default!;

        /// <summary>
        /// The iptv-org channel identifier used for matching, e.g. "France3.fr"
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string IptvChannelId { get; set; } = default!;

        /// <summary>
        /// Direct URL to the logo image file.
        /// </summary>
        [Required]
        [MaxLength(1024)]
        public string Url { get; set; } = default!;

        public int? Width { get; set; }
        public int? Height { get; set; }

        [MaxLength(16)]
        public string? Format { get; set; }

        /// <summary>
        /// Descriptive tags stored as JSON array, e.g. ["horizontal", "white"]
        /// </summary>
        public List<string> Tags { get; set; } = new();

        public bool InUse { get; set; }

        [MaxLength(128)]
        public string? Feed { get; set; }
    }
}
