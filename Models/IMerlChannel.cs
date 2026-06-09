using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NundoTv_WebAPI.Models
{
    public class IMerlChannel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = default!;

        [MaxLength(256)]
        public string? Category { get; set; }

        [MaxLength(8)]
        public string? Country { get; set; }

        [MaxLength(1024)]
        public string? StreamUrl { get; set; }

        [MaxLength(1024)]
        public string? Logo { get; set; }

        public bool IsNsfw { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
