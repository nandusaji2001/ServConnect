using MongoDB.Driver;
using ServConnect.Models.Community;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServConnect.Services
{
    /// <summary>
    /// Service for propagating trust score penalties through social graph when users are banned
    /// Integrates with Python ML Trust Propagation API
    /// </summary>
    public interface ITrustPropagationService
    {
        Task<TrustPropagationResult> PropagateBanPenaltyAsync(Guid bannedUserId, int maxHops = 2, double basePenalty = 0.15);
        Task UpdateUserTrustScoresAsync(Dictionary<string, TrustScoreUpdate> updates);
        Task<bool> BuildSocialGraphAsync();
    }

    public class TrustPropagationService : ITrustPropagationService
    {
        private readonly IMongoCollection<CommunityProfile> _profiles;
        private readonly IMongoCollection<UserFollow> _follows;
        private readonly IMongoCollection<PostLike> _likes;
        private readonly IMongoCollection<PostComment> _comments;
        private readonly IMongoCollection<CommunityPost> _posts;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TrustPropagationService> _logger;
        private readonly string _apiBaseUrl;

        public TrustPropagationService(
            IConfiguration configuration,
            ILogger<TrustPropagationService> logger)
        {
            var conn = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var database = client.GetDatabase(dbName);

            _profiles = database.GetCollection<CommunityProfile>("CommunityProfiles");
            _follows = database.GetCollection<UserFollow>("UserFollows");
            _likes = database.GetCollection<PostLike>("PostLikes");
            _comments = database.GetCollection<PostComment>("PostComments");
            _posts = database.GetCollection<CommunityPost>("CommunityPosts");

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            _logger = logger;
            _apiBaseUrl = configuration["MLServices:TrustPropagationApi"] ?? "http://localhost:8006";
        }

        public async Task<bool> BuildSocialGraphAsync()
        {
            try
            {
                _logger.LogInformation("Building social graph for trust propagation...");

                // Get all follower relationships
                var followers = await _follows.Find(_ => true).ToListAsync();
                var followerData = followers.Select(f => new
                {
                    follower_id = f.FollowerId.ToString(),
                    following_id = f.FollowingId.ToString()
                }).ToList();

                // Build post author lookup to attach likes/comments to actual target users.
                var posts = await _posts.Find(_ => true).ToListAsync();
                var postAuthorById = posts.ToDictionary(p => p.Id, p => p.AuthorId);

                // Get user interactions (likes and comments)
                var interactions = new List<object>();

                // Get likes grouped by user and post owner
                var likes = await _likes.Find(_ => true).ToListAsync();
                var likesByUser = likes
                    .Where(l => postAuthorById.ContainsKey(l.PostId))
                    .GroupBy(l => new
                    {
                        l.UserId,
                        TargetUserId = postAuthorById[l.PostId]
                    });

                foreach (var group in likesByUser)
                {
                    var likeWeight = Math.Min(1.0, 0.55 + (group.Count() - 1) * 0.05);
                    interactions.Add(new
                    {
                        user_id = group.Key.UserId.ToString(),
                        target_user_id = group.Key.TargetUserId.ToString(),
                        type = "like",
                        weight = likeWeight
                    });
                }

                // Get comments grouped by user and post owner
                var comments = await _comments.Find(_ => true).ToListAsync();
                var commentsByUser = comments
                    .Where(c => postAuthorById.ContainsKey(c.PostId))
                    .GroupBy(c => new
                    {
                        c.AuthorId,
                        TargetUserId = postAuthorById[c.PostId]
                    });

                foreach (var group in commentsByUser)
                {
                    var commentWeight = Math.Min(1.0, 0.65 + (group.Count() - 1) * 0.06);
                    interactions.Add(new
                    {
                        user_id = group.Key.AuthorId.ToString(),
                        target_user_id = group.Key.TargetUserId.ToString(),
                        type = "comment",
                        weight = commentWeight
                    });
                }

                // Get current trust scores
                var profiles = await _profiles.Find(_ => true).ToListAsync();
                var trustScores = profiles.ToDictionary(
                    p => p.UserId.ToString(),
                    p => p.UserTrustScore
                );

                // Build request
                var request = new
                {
                    followers = followerData,
                    interactions = interactions,
                    trust_scores = trustScores
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/build-graph", content);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Social graph built successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building social graph");
                return false;
            }
        }

        public async Task<TrustPropagationResult> PropagateBanPenaltyAsync(
            Guid bannedUserId, 
            int maxHops = 2, 
            double basePenalty = 0.15)
        {
            try
            {
                _logger.LogInformation($"Propagating ban penalty for user {bannedUserId}");

                // First, ensure graph is built
                var graphBuilt = await BuildSocialGraphAsync();
                if (!graphBuilt)
                {
                    return new TrustPropagationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to build social graph before propagation."
                    };
                }

                // Call Python API to propagate penalties
                var request = new
                {
                    banned_user_id = bannedUserId.ToString(),
                    max_hops = maxHops,
                    base_penalty = basePenalty
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/propagate-ban", content);
                var responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"Trust propagation API returned {(int)response.StatusCode} {response.StatusCode}. Body: {responseJson}";
                    _logger.LogWarning(errorMessage);
                    return new TrustPropagationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    };
                }

                var result = JsonSerializer.Deserialize<TrustPropagationApiResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true && result.AffectedUsers != null)
                {
                    // Update trust scores in database
                    await UpdateUserTrustScoresAsync(result.AffectedUsers);

                    _logger.LogInformation($"Trust propagation completed. Affected {result.AffectedUsers.Count} users");

                    return new TrustPropagationResult
                    {
                        Success = true,
                        AffectedUserCount = result.AffectedUsers.Count,
                        AffectedUsers = result.AffectedUsers,
                        Summary = result.Summary
                    };
                }

                return new TrustPropagationResult
                {
                    Success = false,
                    ErrorMessage = $"Unexpected trust propagation API response: {responseJson}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error propagating ban penalty");
                return new TrustPropagationResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        public async Task UpdateUserTrustScoresAsync(Dictionary<string, TrustScoreUpdate> updates)
        {
            try
            {
                var bulkOps = new List<WriteModel<CommunityProfile>>();

                foreach (var (userIdStr, update) in updates)
                {
                    if (Guid.TryParse(userIdStr, out var userId))
                    {
                        var filter = Builders<CommunityProfile>.Filter.Eq(p => p.UserId, userId);
                        var updateDef = Builders<CommunityProfile>.Update
                            .Set(p => p.UserTrustScore, update.NewUserTrustScore)
                            .Set(p => p.ContentTrustScore, update.NewContentTrustScore)
                            .Set(p => p.LastTrustScoreUpdate, DateTime.UtcNow);

                        bulkOps.Add(new UpdateOneModel<CommunityProfile>(filter, updateDef));
                    }
                }

                if (bulkOps.Any())
                {
                    await _profiles.BulkWriteAsync(bulkOps);
                    _logger.LogInformation($"Updated trust scores for {bulkOps.Count} users");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user trust scores");
                throw;
            }
        }
    }

    // DTOs
    public class TrustPropagationResult
    {
        public bool Success { get; set; }
        public int AffectedUserCount { get; set; }
        public Dictionary<string, TrustScoreUpdate>? AffectedUsers { get; set; }
        public Dictionary<string, object>? Summary { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class TrustScoreUpdate
    {
        [JsonPropertyName("new_user_trust_score")]
        public double NewUserTrustScore { get; set; }

        [JsonPropertyName("new_content_trust_score")]
        public double NewContentTrustScore { get; set; }

        [JsonPropertyName("penalty_applied")]
        public double PenaltyApplied { get; set; }

        [JsonPropertyName("distance")]
        public int Distance { get; set; }

        [JsonPropertyName("relationship_type")]
        public string RelationshipType { get; set; } = string.Empty;

        [JsonPropertyName("connection_weight")]
        public double ConnectionWeight { get; set; }
    }

    public class TrustPropagationApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("affected_users")]
        public Dictionary<string, TrustScoreUpdate>? AffectedUsers { get; set; }

        [JsonPropertyName("summary")]
        public Dictionary<string, object>? Summary { get; set; }
    }
}
