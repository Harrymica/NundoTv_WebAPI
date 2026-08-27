using System;

namespace NundoTv_WebAPI.Models
{
    public class StreamLink
    {
        public int Id { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = true;
        public string StreamType { get; set; } = "Direct";
        public bool RequiresChannelSearch { get; set; } = false;
        public string? ChannelResolverKey { get; set; }
        public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
