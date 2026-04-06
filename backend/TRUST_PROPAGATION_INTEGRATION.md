# Trust Score Propagation - Integration Complete

## What Was Implemented

A comprehensive trust score propagation system that automatically penalizes users connected to banned users through the social graph.

## Key Features

### 1. Dual Trust Scores

Added to `CommunityProfile.cs`:
- **UserTrustScore** (0-1): Overall user trustworthiness
- **ContentTrustScore** (0-1): Content moderation strictness level
- **LastTrustScoreUpdate**: Timestamp of last update

### 2. Graph-Based Propagation

When a user is banned:
- Direct followers receive ~12% trust penalty
- Friends of friends receive ~4% trust penalty
- Content trust score increases (stricter moderation)
- Penalties decay with distance

### 3. Relationship-Aware Penalties

Different connection types have different weights:
- **Followers**: 0.8 (highest penalty)
- **Mutual follows**: 0.8
- **Commenters**: 0.7
- **Likers**: 0.6
- **Indirect connections**: 0.3-0.5

### 4. Trust Recovery

Users can recover trust over time:
- Default: 1% per day
- Configurable recovery rate
- Gradual return to normal moderation

## Files Created

### Python ML Services

1. **backend/ML/trust_propagation_service.py**
   - Core graph traversal algorithm
   - Penalty calculation logic
   - Trust score computation

2. **backend/ML/trust_propagation_api.py**
   - FastAPI REST service (port 8006)
   - Endpoints for graph building and propagation
   - Integration with C# backend

3. **backend/ML/test_trust_propagation.py**
   - Comprehensive test suite
   - Validates propagation logic
   - Tests distance decay and recovery

4. **backend/ML/start_trust_propagation_api.bat**
   - Windows batch file to start API
   - Easy deployment

### C# Backend Services

5. **backend/Services/TrustPropagationService.cs**
   - C# integration service
   - Fetches social graph from MongoDB
   - Calls Python API
   - Updates trust scores in database

### Documentation

6. **backend/ML/TRUST_PROPAGATION_README.md**
   - Complete system documentation
   - API reference
   - Integration guide
   - Configuration options

7. **backend/TRUST_PROPAGATION_INTEGRATION.md** (this file)
   - Integration summary
   - Usage instructions

## Files Modified

1. **backend/Models/Community/CommunityProfile.cs**
   - Added UserTrustScore field
   - Added ContentTrustScore field
   - Added LastTrustScoreUpdate field

2. **backend/Services/CommunityService.cs**
   - Added ShouldPropagateTrustPenalty to BanResult
   - Added PropagateTrustScorePenaltyAsync method
   - Integrated with ban workflow

## How to Use

### Step 1: Start the Trust Propagation API

```bash
cd backend/ML
python trust_propagation_api.py
```

Or use the batch file:
```bash
cd backend/ML
start_trust_propagation_api.bat
```

API will be available at: http://localhost:8006
Documentation at: http://localhost:8006/docs

### Step 2: Register Service in Program.cs

Add to your dependency injection configuration:

```csharp
// In Program.cs
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITrustPropagationService, TrustPropagationService>();
```

### Step 3: Configure API Endpoint

Add to `appsettings.json`:

```json
{
  "MLServices": {
    "ContentModerationApi": "http://localhost:8001",
    "IntelligentModerationApi": "http://localhost:8002",
    "TrustPropagationApi": "http://localhost:8006"
  }
}
```

### Step 4: Integrate with Ban Workflow

The system is already integrated! When a user is banned in `CommunityService.RecordViolationAsync()`, the `BanResult` will have `ShouldPropagateTrustPenalty = true`.

To trigger propagation, add this to your controller after banning:

```csharp
// In CommunityController or AdminController
var banResult = await _community.RecordViolationAsync(userId, content, contentType, toxicityScore, reason);

if (banResult.WasBanned && banResult.ShouldPropagateTrustPenalty)
{
    // Propagate trust penalties
    var propagationResult = await _trustPropagation.PropagateBanPenaltyAsync(
        bannedUserId: userId,
        maxHops: 2,          // Propagate up to 2 hops away
        basePenalty: 0.15    // 15% base penalty
    );
    
    if (propagationResult.Success)
    {
        _logger.LogInformation($"Trust propagation affected {propagationResult.AffectedUserCount} users");
    }
}
```

### Step 5: Use Trust Scores in Content Moderation

Integrate with your existing content moderation:

```csharp
// In ContentModerationService or EnhancedContentModerationService
public async Task<ModerationResult> ModerateContentAsync(Guid userId, string content, List<string> mediaUrls)
{
    // Get user's trust scores
    var profile = await _community.GetProfileByUserIdAsync(userId);
    
    // Adjust moderation threshold based on content trust score
    // Higher content trust = stricter moderation
    double baseThreshold = 0.7;
    double adjustedThreshold = baseThreshold * (1.0 - profile.ContentTrustScore * 0.3);
    
    // Example: If ContentTrustScore = 0.8 (high scrutiny)
    // adjustedThreshold = 0.7 * (1.0 - 0.8 * 0.3) = 0.7 * 0.76 = 0.532
    // This means content is flagged more easily
    
    var result = await _moderationApi.CheckContentAsync(content, mediaUrls);
    
    if (result.ToxicityScore > adjustedThreshold)
    {
        // Flag content
        return new ModerationResult 
        { 
            IsHarmful = true,
            Reason = $"Content flagged (score: {result.ToxicityScore}, threshold: {adjustedThreshold})"
        };
    }
    
    return new ModerationResult { IsHarmful = false };
}
```

## Testing

### Run Python Tests

```bash
cd backend/ML
python test_trust_propagation.py
```

Expected output:
```
TEST 1: Basic Trust Score Propagation
Affected 4 users:
user2:
  Distance: 1 hops from banned user
  Relationship: follower
  User Trust Score: 0.58 (penalty: -0.12)
  Content Trust Score: 0.68 (stricter moderation)
...
```

### Test API Manually

```bash
# Test health check
curl http://localhost:8006/

# Test graph building
curl -X POST http://localhost:8006/build-graph \
  -H "Content-Type: application/json" \
  -d '{
    "followers": [{"follower_id": "user1", "following_id": "user2"}],
    "interactions": [],
    "trust_scores": {"user1": 0.8, "user2": 0.7}
  }'

# Test ban propagation
curl -X POST http://localhost:8006/propagate-ban \
  -H "Content-Type: application/json" \
  -d '{
    "banned_user_id": "user2",
    "max_hops": 2,
    "base_penalty": 0.15
  }'
```

## Configuration Options

### Adjust Penalty Severity

In your code when calling `PropagateBanPenaltyAsync`:

```csharp
// Mild penalties (for first-time offenders)
await _trustPropagation.PropagateBanPenaltyAsync(userId, maxHops: 1, basePenalty: 0.10);

// Standard penalties (default)
await _trustPropagation.PropagateBanPenaltyAsync(userId, maxHops: 2, basePenalty: 0.15);

// Severe penalties (for repeat offenders or severe violations)
await _trustPropagation.PropagateBanPenaltyAsync(userId, maxHops: 3, basePenalty: 0.25);
```

### Adjust Propagation Distance

- **maxHops = 1**: Only direct connections (followers, commenters)
- **maxHops = 2**: Friends of friends (recommended)
- **maxHops = 3**: Extended network (may affect too many users)

## Monitoring

### Check Trust Scores

Query MongoDB to see trust scores:

```javascript
db.CommunityProfiles.find({
  UserTrustScore: { $lt: 0.6 }
}).sort({ UserTrustScore: 1 })
```

### View Affected Users

After propagation, check logs:

```
[INFO] Trust propagation completed. Affected 15 users
[INFO] Summary: {"total_affected": 15, "by_distance": {"1": 8, "2": 7}}
```

## Benefits

1. **Accountability**: Users think twice about who they follow
2. **Viral Prevention**: Stops harmful content from spreading through networks
3. **Fair**: Penalties decrease with distance
4. **Recoverable**: Trust scores can improve over time
5. **Automated**: No manual intervention needed

## Example Scenario

```
Scenario: User A posts hate speech and gets banned

Before Ban:
- User A: Trust 0.3, Content Trust 0.5
- User B (follower): Trust 0.8, Content Trust 0.5
- User C (follower of B): Trust 0.8, Content Trust 0.5

After Ban Propagation:
- User A: BANNED
- User B: Trust 0.68 (-0.12), Content Trust 0.68 (stricter)
- User C: Trust 0.76 (-0.04), Content Trust 0.56 (slightly stricter)

Result:
- User B's posts now face 20% stricter moderation
- User C's posts face 10% stricter moderation
- Both users are incentivized to unfollow harmful accounts
```

## Next Steps

1. **Monitor Impact**: Track how many users are affected per ban
2. **Tune Parameters**: Adjust penalties based on community feedback
3. **Add UI**: Show trust scores to users in their profile
4. **Recovery System**: Implement automatic trust recovery over time
5. **Analytics**: Build dashboard to visualize trust score distribution

## Support

For issues or questions:
1. Check logs in console output
2. Verify API is running: http://localhost:8006/docs
3. Test with `test_trust_propagation.py`
4. Review `TRUST_PROPAGATION_README.md` for detailed docs
