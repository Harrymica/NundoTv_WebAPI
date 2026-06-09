namespace NundoTv_WebAPI.Models
{
    public class Interest
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public List<UserInterest> UserInterests { get; set; } = new();

    }
}
