using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServConnect.Services
{
    public interface IRatingService
    {
        Task SubmitAsync(string userId, string serviceKey, int rating);
        Task<Dictionary<string, (decimal average, int count)>> GetAveragesAsync(IEnumerable<string> serviceKeys);
        string ComposeKey(string source, string id);
        string DetectKeyFrom(string? id, string? mapUrl);
    }
}