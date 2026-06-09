using System.Text.Json.Serialization;

namespace NundoTv_WebAPI.Models
{
    /// <summary>
    /// Maps a single entry from https://iptv-org.github.io/api/channels.json
    /// </summary>
    public class IptvChannelDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("alt_names")]
        public List<string>? AltNames { get; set; }

        [JsonPropertyName("network")]
        public string? Network { get; set; }

        [JsonPropertyName("owners")]
        public List<string>? Owners { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("categories")]
        public List<string>? Categories { get; set; }

        [JsonPropertyName("is_nsfw")]
        public bool IsNsfw { get; set; }

        [JsonPropertyName("launched")]
        public string? Launched { get; set; }

        [JsonPropertyName("closed")]
        public string? Closed { get; set; }

        [JsonPropertyName("replaced_by")]
        public string? ReplacedBy { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }
    }

    /// <summary>
    /// Maps a single entry from https://iptv-org.github.io/api/streams.json
    /// </summary>
    public class IptvStreamDto
    {
        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("feed")]
        public string? Feed { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;

        [JsonPropertyName("referrer")]
        public string? Referrer { get; set; }

        [JsonPropertyName("user_agent")]
        public string? UserAgent { get; set; }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }
    }

    /// <summary>
    /// Maps a single entry from https://iptv-org.github.io/api/blocklist.json
    /// </summary>
    public class IptvBlocklistEntryDto
    {
        [JsonPropertyName("channel")]
        public string Channel { get; set; } = default!;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = default!;

        [JsonPropertyName("ref")]
        public string? Ref { get; set; }
    }

    /// <summary>
    /// Maps a single entry from https://iptv-org.github.io/api/categories.json
    /// </summary>
    public class IptvCategoryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Response returned from the sync endpoint
    /// </summary>
    public class SyncResultDto
    {
        public int TotalFetched { get; set; }
        public int Stored { get; set; }
        public int Blocked { get; set; }
        public int StreamsMatched { get; set; }
        public string Status { get; set; } = "completed";
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Maps a single entry from https://iptv-org.github.io/api/logos.json
    /// </summary>
    public class IptvLogoDto
    {
        [JsonPropertyName("channel")]
        public string Channel { get; set; } = default!;

        [JsonPropertyName("feed")]
        public string? Feed { get; set; }

        [JsonPropertyName("in_use")]
        public bool InUse { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;
    }

    /// <summary>
    /// Response returned from the logo sync endpoint
    /// </summary>
    public class LogoSyncResultDto
    {
        public int TotalFetched { get; set; }
        public int ChannelLogosStored { get; set; }
        public int BlockedChannelLogosStored { get; set; }
        public int Unmatched { get; set; }
        public string Status { get; set; } = "completed";
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    }
}
