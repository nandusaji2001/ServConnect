using Microsoft.AspNetCore.Mvc;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    /// <summary>
    /// Example integration of Trust Propagation System
    /// This shows how to integrate trust propagation when banning users
    /// </summary>
    public class TrustPropagationExample : Controller
    {
        private readonly ICommunityService _community;
        private readonly ITrustPropagationService _trustPropagation;
        private readonly ILogger<TrustPropagationExample> _logger;

        public TrustPropagationExample(
            ICommunityService community,
            ITrustPropagationService trustPropagation,
            ILogger<TrustPropagationExample> logger)
        {
            _community = community;
            _trustPropagation = trustPropagation;
            _logger = logger;
        }

        /// <summary>
        /// Example 1: Basic integration when banning a user
        /// </summary>
        [HttpPost("api/example/ban-user-with-propagation")]
        public async Task<IActionResult> BanUserWithPropagation(
            Guid userId, 
            string content, 
            string contentType, 
            double toxicityScore, 
            string reason)
        {
            try
            {
                // Step 1: Record violation and potentially ban user
                var banResult = await _community.RecordViolationAsync(
                    userId, 
                    content, 
                    contentType, 
                    toxicityScore, 
                    reason
                );

                // Step 2: If user was banned, propagate trust penalties
                if (banResult.WasBanned && banResult.ShouldPropagateTrustPenalty)
                {
                    _logger.LogInformation($"User {userId} was banned. Propagating trust penalties...");

                    // Propagate penalties through social graph
                    var propagationResult = await _trustPropagation.PropagateBanPenaltyAsync(
                        bannedUserId: userId,
                        maxHops: 2,          // Propagate up to 2 hops (friends of friends)
                        basePenalty: 0.15    // 15% base penalty
                    );

                    if (propagationResult.Success)
                    {
                        _logger.LogInformation(
                            $"Trust propagation completed. Affected {propagationResult.AffectedUserCount} users. " +
                            $"Summary: {System.Text.Json.JsonSerializer.Serialize(propagationResult.Summary)}"
                        );

                        return Ok(new
                        {
                            success = true,
                            message = $"User banned. {propagationResult.AffectedUserCount} connected users affected.",
                            banResult = new
                            {
                                wasBanned = banResult.WasBanned,
                                banLevel = banResult.BanLevel,
                                banDuration = banResult.BanDuration,
                                isPermanent = banResult.IsPermanent
                            },
                            propagation = new
                            {
                                affectedUsers = propagationResult.AffectedUserCount,
                                summary = propagationResult.Summary
                            }
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"Trust propagation failed: {propagationResult.ErrorMessage}");
                        
                        return Ok(new
                        {
                            success = true,
                            message = "User banned, but trust propagation failed.",
                            banResult,
                            propagationError = propagationResult.ErrorMessage
                        });
                    }
                }

                // User was not banned (not enough violations yet)
                return Ok(new
                {
                    success = true,
                    message = $"Violation recorded. User has {banResult.ViolationCount} violations.",
                    banResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ban user with propagation");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Example 2: Graduated penalties based on violation severity
        /// </summary>
        [HttpPost("api/example/ban-with-severity")]
        public async Task<IActionResult> BanWithSeverity(
            Guid userId, 
            string content, 
            string contentType, 
            double toxicityScore, 
            string reason,
            string severity = "standard") // mild, standard, severe
        {
            try
            {
                var banResult = await _community.RecordViolationAsync(
                    userId, content, contentType, toxicityScore, reason
                );

                if (banResult.WasBanned && banResult.ShouldPropagateTrustPenalty)
                {
                    // Adjust penalties based on severity
                    int maxHops;
                    double basePenalty;

                    switch (severity.ToLower())
                    {
                        case "mild":
                            maxHops = 1;
                            basePenalty = 0.10;
                            _logger.LogInformation("Applying mild trust penalties");
                            break;
                        
                        case "severe":
                            maxHops = 3;
                            basePenalty = 0.25;
                            _logger.LogInformation("Applying severe trust penalties");
                            break;
                        
                        default: // standard
                            maxHops = 2;
                            basePenalty = 0.15;
                            _logger.LogInformation("Applying standard trust penalties");
                            break;
                    }

                    var propagationResult = await _trustPropagation.PropagateBanPenaltyAsync(
                        userId, maxHops, basePenalty
                    );

                    return Ok(new
                    {
                        success = true,
                        message = $"User banned with {severity} penalties",
                        affectedUsers = propagationResult.AffectedUserCount,
                        severity,
                        maxHops,
                        basePenalty
                    });
                }

                return Ok(new { success = true, message = "Violation recorded", banResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ban with severity");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Example 3: Check user's trust scores
        /// </summary>
        [HttpGet("api/example/trust-scores/{userId}")]
        public async Task<IActionResult> GetTrustScores(Guid userId)
        {
            try
            {
                var profile = await _community.GetProfileByUserIdAsync(userId);
                
                if (profile == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                return Ok(new
                {
                    userId = profile.UserId,
                    username = profile.Username,
                    userTrustScore = profile.UserTrustScore,
                    contentTrustScore = profile.ContentTrustScore,
                    lastUpdate = profile.LastTrustScoreUpdate,
                    interpretation = new
                    {
                        trustLevel = profile.UserTrustScore switch
                        {
                            >= 0.8 => "High Trust",
                            >= 0.6 => "Medium Trust",
                            >= 0.4 => "Low Trust",
                            _ => "Very Low Trust"
                        },
                        moderationLevel = profile.ContentTrustScore switch
                        {
                            >= 0.7 => "Strict Moderation",
                            >= 0.5 => "Standard Moderation",
                            _ => "Relaxed Moderation"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trust scores");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Example 4: Adjust content moderation based on trust scores
        /// </summary>
        [HttpPost("api/example/moderate-with-trust")]
        public async Task<IActionResult> ModerateWithTrust(
            Guid userId,
            string content,
            double toxicityScore)
        {
            try
            {
                // Get user's trust scores
                var profile = await _community.GetProfileByUserIdAsync(userId);
                
                if (profile == null)
                {
                    return BadRequest(new { error = "User not found" });
                }

                // Base threshold for flagging content
                double baseThreshold = 0.7;

                // Adjust threshold based on content trust score
                // Higher content trust = stricter moderation (lower threshold)
                double adjustedThreshold = baseThreshold * (1.0 - profile.ContentTrustScore * 0.3);

                // Check if content should be flagged
                bool shouldFlag = toxicityScore > adjustedThreshold;

                return Ok(new
                {
                    userId,
                    toxicityScore,
                    baseThreshold,
                    adjustedThreshold,
                    contentTrustScore = profile.ContentTrustScore,
                    shouldFlag,
                    message = shouldFlag 
                        ? $"Content flagged (score {toxicityScore:F2} > threshold {adjustedThreshold:F2})"
                        : $"Content approved (score {toxicityScore:F2} <= threshold {adjustedThreshold:F2})",
                    explanation = profile.ContentTrustScore > 0.6
                        ? "User has high content trust score, facing stricter moderation"
                        : "User has normal content trust score"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in moderate with trust");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Example 5: Build social graph manually (for testing)
        /// </summary>
        [HttpPost("api/example/build-graph")]
        public async Task<IActionResult> BuildGraph()
        {
            try
            {
                _logger.LogInformation("Building social graph...");
                
                var success = await _trustPropagation.BuildSocialGraphAsync();
                
                if (success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Social graph built successfully"
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Failed to build social graph"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building graph");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Example 6: Get propagation statistics
        /// </summary>
        [HttpGet("api/example/propagation-stats")]
        public async Task<IActionResult> GetPropagationStats()
        {
            try
            {
                // Get all profiles with trust scores
                var profiles = await _community.GetProfileByUserIdAsync(Guid.Empty); // This is just an example
                
                // In a real implementation, you'd query all profiles
                // and compute statistics
                
                return Ok(new
                {
                    message = "This is an example endpoint",
                    note = "Implement actual statistics gathering based on your needs",
                    exampleStats = new
                    {
                        totalUsers = 1000,
                        averageTrustScore = 0.72,
                        usersWithLowTrust = 150,
                        usersWithHighContentTrust = 80
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
