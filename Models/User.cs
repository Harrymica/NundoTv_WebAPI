namespace NundoTv_WebAPI.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string ClerkId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }

        public bool HasCompletedOnboarding { get; set; } = false;
        public bool HasConsentedToAds { get; set; } = false;
        public bool HasConsentedToDataSharing { get; set; } = false;
        public bool IsPremiumAdFree { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<UserInterest> UserInterests { get; set; } = new();
    }
}
