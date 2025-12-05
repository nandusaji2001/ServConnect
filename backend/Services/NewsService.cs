using System.Xml.Linq;

namespace ServConnect.Services
{
    public class NewsService : INewsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsService> _logger;

        public NewsService(HttpClient httpClient, ILogger<NewsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<NewsResponse> GetNewsByLocationAsync(string location)
        {
            var response = new NewsResponse();

            try
            {
                var rssUrl = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(location)}&hl=en-US&gl=US&ceid=US:en";
                
                var httpResponse = await _httpClient.GetAsync(rssUrl);
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch news for location: {location}");
                    return response;
                }

                var content = await httpResponse.Content.ReadAsStringAsync();
                var xdoc = XDocument.Parse(content);

                var items = xdoc.Descendants("item").Take(8).ToList();

                foreach (var item in items)
                {
                    try
                    {
                        var title = item.Element("title")?.Value ?? string.Empty;
                        var description = item.Element("description")?.Value ?? string.Empty;
                        var link = item.Element("link")?.Value ?? string.Empty;
                        var pubDateStr = item.Element("pubDate")?.Value ?? string.Empty;
                        var source = item.Element("source")?.Attribute("url")?.Value ?? "Google News";

                        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description))
                            continue;

                        DateTime? pubDate = null;
                        if (!string.IsNullOrEmpty(pubDateStr) && DateTime.TryParse(pubDateStr, out var parsed))
                        {
                            pubDate = parsed;
                        }

                        var imageUrl = ExtractImageUrl(description);

                        var article = new NewsArticle
                        {
                            Title = StripHtmlTags(title),
                            Description = StripHtmlTags(description),
                            Link = link,
                            Source = source,
                            ImageUrl = imageUrl,
                            PubDate = pubDate
                        };

                        response.Articles.Add(article);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error parsing news item: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching news for location {location}: {ex.Message}");
            }

            return response;
        }

        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = System.Text.RegularExpressions.Regex.Replace(input, "<[^>]*>", string.Empty);
            result = System.Net.WebUtility.HtmlDecode(result);
            return result.Trim();
        }

        private static string ExtractImageUrl(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                return string.Empty;

            var imgMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<img[^>]+src=[""']?([^""'>\s]+)[""']?");
            if (imgMatch.Success)
            {
                return imgMatch.Groups[1].Value;
            }

            return string.Empty;
        }
    }
}
