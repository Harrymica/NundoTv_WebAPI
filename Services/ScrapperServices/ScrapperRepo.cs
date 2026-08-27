namespace NundoTv_WebAPI.Services.ScrapperServices
{
    public class ScrapperRepo
    {
        private readonly HttpClient _httpClient;

        public ScrapperRepo(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetFMHYData()
        {
            string sportsUrl = "https://githubusercontent.com";
            using HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Add("User-Agent", "C# Sports Aggregator");
            Console.WriteLine("Connecting to live markdown stream...");

            string rawMarkdown = await client.GetStringAsync(sportsUrl);

            return rawMarkdown;


        }
    }
}
