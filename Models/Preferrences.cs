namespace NundoTv_WebAPI.Models
{
    public class Preferrences
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public Guid InterestId { get; set; }
        public Interest Interest { get; set; } = null!;
    }
}
