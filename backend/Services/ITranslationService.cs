namespace ServConnect.Services
{
    public interface ITranslationService
    {
        Task<string> TranslateTextAsync(string text, string sourceLanguage, string targetLanguage);
    }
}
