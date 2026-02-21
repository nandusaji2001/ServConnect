using ServConnect.Models;

namespace ServConnect.Services
{
    public interface ITransportUpdateService
    {
        // Route Operations
        Task<TransportRoute> CreateRouteAsync(TransportRoute route);
        Task<TransportRoute?> GetRouteByIdAsync(string id);
        Task<List<TransportRoute>> GetAllRoutesAsync(string? transportType = null, string? district = null, bool activeOnly = true);
        Task<List<TransportRoute>> GetRoutesByContributorAsync(Guid userId);
        Task<List<TransportRoute>> SearchRoutesAsync(string? from = null, string? to = null, string? transportType = null, string? district = null);
        Task<bool> UpdateRouteAsync(TransportRoute route);
        Task<bool> DeleteRouteAsync(string id, Guid userId);

        // Voting Operations
        Task<(bool success, string message, int newScore)> UpvoteRouteAsync(string routeId, Guid userId);
        Task<(bool success, string message, int newScore)> DownvoteRouteAsync(string routeId, Guid userId);
        Task<bool> RemoveVoteAsync(string routeId, Guid userId);
        Task<bool> HasUserVotedAsync(string routeId, Guid userId);
        Task<string?> GetUserVoteTypeAsync(string routeId, Guid userId); // "upvote", "downvote", or null

        // Auto-removal check (called after downvotes)
        Task<bool> CheckAndRemoveIfThresholdReachedAsync(string routeId);

        // Confirmation (users can confirm route is still accurate)
        Task<bool> ConfirmRouteAccuracyAsync(string routeId, Guid userId);

        // Route Update Requests
        Task<RouteUpdateRequest> CreateUpdateRequestAsync(RouteUpdateRequest request);
        Task<List<RouteUpdateRequest>> GetUpdateRequestsForRouteAsync(string routeId);
        Task<bool> SupportUpdateRequestAsync(string requestId, Guid userId);
        Task<bool> ResolveUpdateRequestAsync(string requestId);

        // Saved Routes
        Task<SavedRoute> SaveRouteAsync(SavedRoute savedRoute);
        Task<List<SavedRoute>> GetSavedRoutesAsync(Guid userId);
        Task<bool> RemoveSavedRouteAsync(string savedRouteId, Guid userId);
        Task<bool> IsRouteSavedAsync(string routeId, Guid userId);

        // Statistics
        Task<int> GetTotalRoutesCountAsync(string? district = null);
        Task<int> GetActiveRoutesCountAsync(string? district = null);
        Task<List<TransportRoute>> GetTopRatedRoutesAsync(string? district = null, int count = 10);
        Task<List<TransportRoute>> GetRecentRoutesAsync(string? district = null, int count = 10);
        
        // Location suggestions (get unique locations for autocomplete)
        Task<List<string>> GetPopularLocationsAsync(string? district = null);
    }
}
