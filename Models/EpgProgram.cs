using System.ComponentModel.DataAnnotations;

namespace NundoTv_WebAPI.Models
{
    public class EpgProgram
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(256)]
        public string ChannelId { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? ChannelName { get; set; }

        [Required]
        public DateTime Start { get; set; }

        [Required]
        public DateTime Stop { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(256)]
        public string? Category { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
