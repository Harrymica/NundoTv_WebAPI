using System;
using System.ComponentModel.DataAnnotations;

namespace NundoTv_WebAPI.Models
{
    public class EpgChannelMapping
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(256)]
        public string ChannelId { get; set; } = string.Empty; // NanoId of LiveChannel/LivePremiumChannel

        [Required]
        [MaxLength(256)]
        public string EpgChannelId { get; set; } = string.Empty; // XMLTV ChannelId from EpgProgram

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
