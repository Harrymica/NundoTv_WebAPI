namespace NundoTv_WebAPI.Models
{
    
    //private static readonly string[] BlockedKeywords =
    //        {
    //            "xxx", "porn", "adult", "sex", "18+", "nsfw",
    //            "onlyfans", "hentai", "xxxmovies",
    //            "bet", "casino", "gambling",
    //            "torrent", "pirated", "dmca"
    //        };
    public class BlockedKeyword
    {
        public int Id { get; set; }
        public string Keyword { get; set; } = default!;
    }

}
