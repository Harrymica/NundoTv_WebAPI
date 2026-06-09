namespace NundoTv_WebAPI.Models
{
    

    public class TelemetryEvent
    {
        public long Id { get; set; }

        public string DeviceId { get; set; } = default!;
        public string ChannelName { get; set; } = default!;
        public string Category { get; set; } = default!;

        public DateTime Timestamp { get; set; }

        public int WatchDurationSeconds { get; set; }

        public bool ConsentGiven { get; set; }

        public string DeviceType { get; set; } = default!;
    }
}
