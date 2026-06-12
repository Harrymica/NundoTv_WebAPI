using System.ComponentModel.DataAnnotations;

namespace NundoTv_WebAPI.Models
{
    public class LivePremiumChannel
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        [Required]
        public string StreamUrl { get; set; } = string.Empty;

        public string? YoutubeUrl { get; set; }

        public string? Country { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("Languages")]
        public string LanguagesRaw { get; set; } = "[]";

        [System.ComponentModel.DataAnnotations.Schema.Column("Categories")]
        public string CategoriesRaw { get; set; } = "[]";

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> Languages 
        { 
            get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(LanguagesRaw) ?? new();
            set => LanguagesRaw = System.Text.Json.JsonSerializer.Serialize(value);
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public List<string> Categories 
        { 
            get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(CategoriesRaw) ?? new();
            set => CategoriesRaw = System.Text.Json.JsonSerializer.Serialize(value);
        }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
