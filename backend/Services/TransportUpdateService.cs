using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class TransportUpdateService : ITransportUpdateService
    {
        private readonly IMongoCollection<TransportRoute> _routes;
        private readonly IMongoCollection<RouteUpdateRequest> _updateRequests;
        private readonly IMongoCollection<SavedRoute> _savedRoutes;

        public TransportUpdateService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _routes = db.GetCollection<TransportRoute>("TransportRoutes");
            _updateRequests = db.GetCollection<RouteUpdateRequest>("RouteUpdateRequests");
            _savedRoutes = db.GetCollection<SavedRoute>("SavedRoutes");

            // Create indexes for better search performance
            CreateIndexes();
        }

        private void CreateIndexes()
        {
            // Index for location-based searches
            var startLocationIndex = Builders<TransportRoute>.IndexKeys
                .Text(r => r.StartLocation)
                .Text(r => r.EndLocation)
                .Text(r => r.IntermediateStops);
            
            _routes.Indexes.CreateOne(new CreateIndexModel<TransportRoute>(startLocationIndex));

            // Index for district filtering
            var districtIndex = Builders<TransportRoute>.IndexKeys.Ascending(r => r.District);
            _routes.Indexes.CreateOne(new CreateIndexModel<TransportRoute>(districtIndex));
        }

        #region Route Operations

        public async Task<TransportRoute> CreateRouteAsync(TransportRoute route)
        {
            route.CreatedAt = DateTime.UtcNow;
            route.UpdatedAt = route.CreatedAt;
            route.Status = TransportRouteStatus.Active;
            route.Upvotes = 0;
            route.Downvotes = 0;
            await _routes.InsertOneAsync(route);
            return route;
        }

        public async Task<TransportRoute?> GetRouteByIdAsync(string id)
        {
            return await _routes.Find(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<TransportRoute>> GetAllRoutesAsync(string? transportType = null, string? district = null, bool activeOnly = true)
        {
            var filterBuilder = Builders<TransportRoute>.Filter;
            var filter = filterBuilder.Empty;

            if (activeOnly)
                filter &= filterBuilder.Eq(r => r.Status, TransportRouteStatus.Active);

            if (!string.IsNullOrWhiteSpace(transportType))
                filter &= filterBuilder.Eq(r => r.TransportType, transportType);

            if (!string.IsNullOrWhiteSpace(district))
                filter &= filterBuilder.Eq(r => r.District, district);

            return await _routes.Find(filter)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TransportRoute>> GetRoutesByContributorAsync(Guid userId)
        {
            return await _routes.Find(r => r.ContributorId == userId)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TransportRoute>> SearchRoutesAsync(string? from = null, string? to = null, string? transportType = null, string? district = null)
        {
            var filterBuilder = Builders<TransportRoute>.Filter;
            var filter = filterBuilder.Eq(r => r.Status, TransportRouteStatus.Active);

            if (!string.IsNullOrWhiteSpace(from))
            {
                var fromLower = from.ToLower();
                filter &= filterBuilder.Or(
                    filterBuilder.Regex(r => r.StartLocation, new MongoDB.Bson.BsonRegularExpression(from, "i")),
                    filterBuilder.Regex(r => r.StartLocationDetails, new MongoDB.Bson.BsonRegularExpression(from, "i")),
                    filterBuilder.AnyEq(r => r.IntermediateStops, from)
                );
            }

            if (!string.IsNullOrWhiteSpace(to))
            {
                filter &= filterBuilder.Or(
                    filterBuilder.Regex(r => r.EndLocation, new MongoDB.Bson.BsonRegularExpression(to, "i")),
                    filterBuilder.Regex(r => r.EndLocationDetails, new MongoDB.Bson.BsonRegularExpression(to, "i")),
                    filterBuilder.AnyEq(r => r.IntermediateStops, to)
                );
            }

            if (!string.IsNullOrWhiteSpace(transportType))
                filter &= filterBuilder.Eq(r => r.TransportType, transportType);

            if (!string.IsNullOrWhiteSpace(district))
                filter &= filterBuilder.Eq(r => r.District, district);

            return await _routes.Find(filter)
                .SortByDescending(r => r.Upvotes - r.Downvotes)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateRouteAsync(TransportRoute route)
        {
            route.UpdatedAt = DateTime.UtcNow;
            var result = await _routes.ReplaceOneAsync(r => r.Id == route.Id, route);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> DeleteRouteAsync(string id, Guid userId)
        {
            // Only allow deletion by the contributor
            var route = await GetRouteByIdAsync(id);
            if (route == null || route.ContributorId != userId)
                return false;

            var result = await _routes.DeleteOneAsync(r => r.Id == id);
            return result.DeletedCount == 1;
        }

        #endregion

        #region Voting Operations

        public async Task<(bool success, string message, int newScore)> UpvoteRouteAsync(string routeId, Guid userId)
        {
            var route = await GetRouteByIdAsync(routeId);
            if (route == null)
                return (false, "Route not found", 0);

            if (route.Status != TransportRouteStatus.Active)
                return (false, "Cannot vote on inactive routes", route.Score);

            // Check if already upvoted
            if (route.UpvotedBy.Contains(userId))
                return (false, "You have already upvoted this route", route.Score);

            // Remove from downvotes if previously downvoted
            if (route.DownvotedBy.Contains(userId))
            {
                var removeDownvote = Builders<TransportRoute>.Update
                    .Pull(r => r.DownvotedBy, userId)
                    .Inc(r => r.Downvotes, -1);
                await _routes.UpdateOneAsync(r => r.Id == routeId, removeDownvote);
            }

            // Add upvote
            var update = Builders<TransportRoute>.Update
                .AddToSet(r => r.UpvotedBy, userId)
                .Inc(r => r.Upvotes, 1)
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            var result = await _routes.UpdateOneAsync(r => r.Id == routeId, update);
            
            // Get updated route for new score
            route = await GetRouteByIdAsync(routeId);
            return (result.ModifiedCount == 1, "Thank you for your feedback!", route?.Score ?? 0);
        }

        public async Task<(bool success, string message, int newScore)> DownvoteRouteAsync(string routeId, Guid userId)
        {
            var route = await GetRouteByIdAsync(routeId);
            if (route == null)
                return (false, "Route not found", 0);

            if (route.Status != TransportRouteStatus.Active)
                return (false, "Cannot vote on inactive routes", route.Score);

            // Check if already downvoted
            if (route.DownvotedBy.Contains(userId))
                return (false, "You have already downvoted this route", route.Score);

            // Remove from upvotes if previously upvoted
            if (route.UpvotedBy.Contains(userId))
            {
                var removeUpvote = Builders<TransportRoute>.Update
                    .Pull(r => r.UpvotedBy, userId)
                    .Inc(r => r.Upvotes, -1);
                await _routes.UpdateOneAsync(r => r.Id == routeId, removeUpvote);
            }

            // Add downvote
            var update = Builders<TransportRoute>.Update
                .AddToSet(r => r.DownvotedBy, userId)
                .Inc(r => r.Downvotes, 1)
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            var result = await _routes.UpdateOneAsync(r => r.Id == routeId, update);

            // Check if route should be removed
            await CheckAndRemoveIfThresholdReachedAsync(routeId);

            // Get updated route for new score
            route = await GetRouteByIdAsync(routeId);
            return (result.ModifiedCount == 1, "Thank you for your feedback!", route?.Score ?? 0);
        }

        public async Task<bool> RemoveVoteAsync(string routeId, Guid userId)
        {
            var route = await GetRouteByIdAsync(routeId);
            if (route == null) return false;

            var updateBuilder = Builders<TransportRoute>.Update;
            UpdateDefinition<TransportRoute>? update = null;

            if (route.UpvotedBy.Contains(userId))
            {
                update = updateBuilder
                    .Pull(r => r.UpvotedBy, userId)
                    .Inc(r => r.Upvotes, -1)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow);
            }
            else if (route.DownvotedBy.Contains(userId))
            {
                update = updateBuilder
                    .Pull(r => r.DownvotedBy, userId)
                    .Inc(r => r.Downvotes, -1)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow);
            }

            if (update == null) return false;

            var result = await _routes.UpdateOneAsync(r => r.Id == routeId, update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> HasUserVotedAsync(string routeId, Guid userId)
        {
            var route = await GetRouteByIdAsync(routeId);
            if (route == null) return false;
            return route.UpvotedBy.Contains(userId) || route.DownvotedBy.Contains(userId);
        }

        public async Task<string?> GetUserVoteTypeAsync(string routeId, Guid userId)
        {
            var route = await GetRouteByIdAsync(routeId);
            if (route == null) return null;
            if (route.UpvotedBy.Contains(userId)) return "upvote";
            if (route.DownvotedBy.Contains(userId)) return "downvote";
            return null;
        }

        public async Task<bool> CheckAndRemoveIfThresholdReachedAsync(string routeId)
        {
            var route = await GetRouteByIdAsync(routeId);
            if (route == null) return false;

            // Check if downvotes exceed upvotes by the threshold
            if ((route.Downvotes - route.Upvotes) >= TransportRoute.RemovalThreshold)
            {
                var update = Builders<TransportRoute>.Update
                    .Set(r => r.Status, TransportRouteStatus.Removed)
                    .Set(r => r.UpdatedAt, DateTime.UtcNow);

                var result = await _routes.UpdateOneAsync(r => r.Id == routeId, update);
                return result.ModifiedCount == 1;
            }

            return false;
        }

        public async Task<bool> ConfirmRouteAccuracyAsync(string routeId, Guid userId)
        {
            var update = Builders<TransportRoute>.Update
                .Set(r => r.LastConfirmedAt, DateTime.UtcNow)
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            var result = await _routes.UpdateOneAsync(r => r.Id == routeId, update);
            return result.ModifiedCount == 1;
        }

        #endregion

        #region Route Update Requests

        public async Task<RouteUpdateRequest> CreateUpdateRequestAsync(RouteUpdateRequest request)
        {
            request.CreatedAt = DateTime.UtcNow;
            request.IsResolved = false;
            request.SupportingVotes = 1;
            request.SupportedBy = new List<Guid> { request.ReporterId };
            await _updateRequests.InsertOneAsync(request);
            return request;
        }

        public async Task<List<RouteUpdateRequest>> GetUpdateRequestsForRouteAsync(string routeId)
        {
            var requests = await _updateRequests.Find(r => r.RouteId == routeId && !r.IsResolved)
                .SortByDescending(r => r.SupportingVotes)
                .ToListAsync();

            // Load route info for each request
            var route = await GetRouteByIdAsync(routeId);
            foreach (var request in requests)
            {
                request.Route = route;
            }

            return requests;
        }

        public async Task<bool> SupportUpdateRequestAsync(string requestId, Guid userId)
        {
            var request = await _updateRequests.Find(r => r.Id == requestId).FirstOrDefaultAsync();
            if (request == null || request.SupportedBy.Contains(userId))
                return false;

            var update = Builders<RouteUpdateRequest>.Update
                .AddToSet(r => r.SupportedBy, userId)
                .Inc(r => r.SupportingVotes, 1);

            var result = await _updateRequests.UpdateOneAsync(r => r.Id == requestId, update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> ResolveUpdateRequestAsync(string requestId)
        {
            var update = Builders<RouteUpdateRequest>.Update
                .Set(r => r.IsResolved, true)
                .Set(r => r.ResolvedAt, DateTime.UtcNow);

            var result = await _updateRequests.UpdateOneAsync(r => r.Id == requestId, update);
            return result.ModifiedCount == 1;
        }

        #endregion

        #region Saved Routes

        public async Task<SavedRoute> SaveRouteAsync(SavedRoute savedRoute)
        {
            savedRoute.SavedAt = DateTime.UtcNow;
            await _savedRoutes.InsertOneAsync(savedRoute);
            return savedRoute;
        }

        public async Task<List<SavedRoute>> GetSavedRoutesAsync(Guid userId)
        {
            var savedRoutes = await _savedRoutes.Find(s => s.UserId == userId)
                .SortByDescending(s => s.SavedAt)
                .ToListAsync();

            // Load route info for each saved route
            foreach (var saved in savedRoutes)
            {
                saved.Route = await GetRouteByIdAsync(saved.RouteId);
            }

            return savedRoutes;
        }

        public async Task<bool> RemoveSavedRouteAsync(string savedRouteId, Guid userId)
        {
            var result = await _savedRoutes.DeleteOneAsync(s => s.Id == savedRouteId && s.UserId == userId);
            return result.DeletedCount == 1;
        }

        public async Task<bool> IsRouteSavedAsync(string routeId, Guid userId)
        {
            var count = await _savedRoutes.CountDocumentsAsync(s => s.RouteId == routeId && s.UserId == userId);
            return count > 0;
        }

        #endregion

        #region Statistics

        public async Task<int> GetTotalRoutesCountAsync(string? district = null)
        {
            var filter = Builders<TransportRoute>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(district))
                filter &= Builders<TransportRoute>.Filter.Eq(r => r.District, district);

            return (int)await _routes.CountDocumentsAsync(filter);
        }

        public async Task<int> GetActiveRoutesCountAsync(string? district = null)
        {
            var filter = Builders<TransportRoute>.Filter.Eq(r => r.Status, TransportRouteStatus.Active);
            if (!string.IsNullOrWhiteSpace(district))
                filter &= Builders<TransportRoute>.Filter.Eq(r => r.District, district);

            return (int)await _routes.CountDocumentsAsync(filter);
        }

        public async Task<List<TransportRoute>> GetTopRatedRoutesAsync(string? district = null, int count = 10)
        {
            var filter = Builders<TransportRoute>.Filter.Eq(r => r.Status, TransportRouteStatus.Active);
            if (!string.IsNullOrWhiteSpace(district))
                filter &= Builders<TransportRoute>.Filter.Eq(r => r.District, district);

            // Sort by score (upvotes - downvotes) descending
            return await _routes.Find(filter)
                .SortByDescending(r => r.Upvotes - r.Downvotes)
                .ThenByDescending(r => r.Upvotes)
                .Limit(count)
                .ToListAsync();
        }

        public async Task<List<TransportRoute>> GetRecentRoutesAsync(string? district = null, int count = 10)
        {
            var filter = Builders<TransportRoute>.Filter.Eq(r => r.Status, TransportRouteStatus.Active);
            if (!string.IsNullOrWhiteSpace(district))
                filter &= Builders<TransportRoute>.Filter.Eq(r => r.District, district);

            return await _routes.Find(filter)
                .SortByDescending(r => r.CreatedAt)
                .Limit(count)
                .ToListAsync();
        }

        public async Task<List<string>> GetPopularLocationsAsync(string? district = null)
        {
            var filter = Builders<TransportRoute>.Filter.Eq(r => r.Status, TransportRouteStatus.Active);
            if (!string.IsNullOrWhiteSpace(district))
                filter &= Builders<TransportRoute>.Filter.Eq(r => r.District, district);

            var routes = await _routes.Find(filter).ToListAsync();

            var locations = new HashSet<string>();
            foreach (var route in routes)
            {
                if (!string.IsNullOrWhiteSpace(route.StartLocation))
                    locations.Add(route.StartLocation);
                if (!string.IsNullOrWhiteSpace(route.EndLocation))
                    locations.Add(route.EndLocation);
                foreach (var stop in route.IntermediateStops)
                {
                    if (!string.IsNullOrWhiteSpace(stop))
                        locations.Add(stop);
                }
            }

            return locations.OrderBy(l => l).ToList();
        }

        #endregion
    }
}
