using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServConnect.Services
{
    public interface IRecommendationService
    {
        // Returns top-N recommended service names for the user
        Task<List<string>> GetTopServicesForUserAsync(Guid userId, int topN = 3);
    }
}