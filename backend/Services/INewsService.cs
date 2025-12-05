namespace ServConnect.Services
{
    public interface INewsService
    {
        Task<NewsResponse> GetNewsByLocationAsync(string location);
    }

    public class NewsArticle
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime? PubDate { get; set; }
    }

    public class NewsResponse
    {
        public List<NewsArticle> Articles { get; set; } = new();
    }
}
