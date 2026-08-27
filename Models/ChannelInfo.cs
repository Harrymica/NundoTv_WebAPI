namespace NundoTv_WebAPI.Models
{
    public class ChannelInfo
    {
        public string ChannelId { get; set; } = string.Empty;
        public string ChannelName { get; set; } = string.Empty;
        public string PlayableUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
    }
}
