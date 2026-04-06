namespace ServConnect.Services
{
    public interface IChatbotService
    {
        ChatbotResponse GetResponse(string userMessage);
        List<string> GetQuickSuggestions();
    }

    public class ChatbotResponse
    {
        public string Message { get; set; } = string.Empty;
        public string? NavigationUrl { get; set; }
        public string? NavigationLabel { get; set; }
        public List<string> RelatedQuestions { get; set; } = new();
    }
}
