# Trust Score Propagation System

## Overview

The Trust Score Propagation System automatically reduces trust scores for users who interact with banned users in the community platform. This creates a network effect where harmful behavior impacts not just the violator, but also their social connections, incentivizing users to be more careful about who they follow and interact with.

## How It Works

### Core Concept

When a user is banned for posting harmful content (detected via multimodal AI - CLIP + OCR + text analysis):

1. **Direct Connections** (followers, frequent interactors) receive a trust score penalty
2. **Indirect Connections** (friends of friends) receive smaller penalties based on distance
3. **Content Trust Score** increases for affected users (meaning stricter moderation)

### Trust Scores

Each user has two trust scores:

- **User Trust Score** (0-1): How trustworthy the user is
  - Higher = more trustworthy
  - Lower = less trustworthy, may face restrictions
  
- **Content Trust Score** (0-1): How strictly their content is moderated
  - Lower = normal moderation
  - Higher = stricter moderation, more scrutiny on posts

### Penalty Calculation

```
penalty = base_penalty × connection_weight × distance_factor

where:
- base_penalty: 0.15 (default, configurable)
- connection_weight: 0.3-0.8 based on relationship strength
  - Follower: 0.8 (strongest)
  - Mutual follow: 0.8
  - Frequent interactor: 0.6-0.7
  - Indirect connection: 0.3-0.5
- distance_factor: 1 / (distance^1.5)
  - Distance 1 (direct): 1.0
  - Distance 2 (friend of friend): 0.35
  - Distance 3: 0.19
```

### Example Scenario

```
User A posts harmful content → Gets banned
  ↓
User B (follower of A)
  - User Trust: 0.8 → 0.68 (-0.12 penalty)
  - Content Trust: 0.5 → 0.68 (stricter moderation)
  ↓
User C (follower of B, not A)
  - User Trust: 0.8 → 0.76 (-0.04 penalty)
  - Content Trust: 0.5 → 0.56 (slightly stricter)
```

## Architecture

### Components

1. **trust_propagation_service.py** - Core Python service
   - Graph traversal algorithm
   - Penalty calculation
   - Trust score updates

2. **trust_propagation_api.py** - FastAPI REST API
   - Endpoints for building graph and propagating penalties
   - Runs on port 8006

3. **TrustPropagationService.cs** - C# integration service
   - Fetches follower/interaction data from MongoDB
   - Calls Python API
   - Updates trust scores in database

4. **CommunityProfile.cs** - Extended with trust score fields
   - UserTrustScore
   - ContentTrustScore
   - LastTrustScoreUpdate

### Data Flow

```
User Banned (CommunityService)
    ↓
Build Social Graph (followers + interactions)
    ↓
Call Python API (/propagate-ban)
    ↓
Compute Affected Users (graph traversal)
    ↓
Update Trust Scores in MongoDB
    ↓
Apply Stricter Moderation (ContentModerationService)
```

## API Reference

### Start the API

```bash
cd backend/ML
python trust_propagation_api.py
# or
start_trust_propagation_api.bat
```

API available at: http://localhost:8006
Documentation: http://localhost:8006/docs

### Endpoints

#### 1. Build Social Graph

```http
POST /build-graph
Content-Type: application/json

{
  "followers": [
    {"follower_id": "user1", "following_id": "user2"}
  ],
  "interactions": [
    {"user_id": "user1", "target_user_id": "user2", "type": "like", "weight": 0.6}
  ],
  "trust_scores": {
    "user1": 0.8,
    "user2": 0.7
  }
}
```

#### 2. Propagate Ban Penalty

```http
POST /propagate-ban
Content-Type: application/json

{
  "banned_user_id": "user123",
  "max_hops": 2,
  "base_penalty": 0.15
}
```

Response:
```json
{
  "success": true,
  "affected_users": {
    "user456": {
      "new_user_trust_score": 0.68,
      "new_content_trust_score": 0.68,
      "penalty_applied": 0.12,
      "distance": 1,
      "relationship_type": "follower",
      "connection_weight": 0.8
    }
  },
  "summary": {
    "total_affected": 5,
    "by_distance": {"1": 3, "2": 2},
    "by_relationship": {"follower": 3, "indirect_connection": 2},
    "avg_penalty": 0.08
  }
}
```

#### 3. Compute Recovery

```http
POST /compute-recovery
Content-Type: application/json

{
  "user_id": "user456",
  "days_since_penalty": 30,
  "recovery_rate": 0.01
}
```

## Integration Guide

### Step 1: Add to appsettings.json

```json
{
  "MLServices": {
    "TrustPropagationApi": "http://localhost:8006"
  }
}
```

### Step 2: Register Service in Program.cs

```csharp
builder.Services.AddScoped<ITrustPropagationService, TrustPropagationService>();
```

### Step 3: Use in CommunityService

```csharp
// After banning a user
var banResult = await RecordViolationAsync(userId, content, contentType, toxicityScore, reason);

if (banResult.ShouldPropagateTrustPenalty)
{
    // Propagate trust penalties through social graph
    await _trustPropagation.PropagateBanPenaltyAsync(
        bannedUserId: userId,
        maxHops: 2,
        basePenalty: 0.15
    );
}
```

### Step 4: Use Trust Scores in Content Moderation

```csharp
// In ContentModerationService
var profile = await _community.GetProfileByUserIdAsync(userId);

// Adjust moderation threshold based on content trust score
double baseThreshold = 0.7;
double adjustedThreshold = baseThreshold * (1.0 - profile.ContentTrustScore * 0.3);

// Users with higher content trust score face stricter moderation
if (toxicityScore > adjustedThreshold)
{
    // Flag content
}
```

## Configuration

### Tunable Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| max_hops | 2 | Maximum propagation distance (1-3) |
| base_penalty | 0.15 | Base penalty for direct connections (0-0.5) |
| recovery_rate | 0.01 | Daily trust score recovery rate (0-0.1) |

### Relationship Weights

| Relationship | Weight | Description |
|--------------|--------|-------------|
| Follower | 0.8 | User follows banned user |
| Mutual Follow | 0.8 | Both users follow each other |
| Comment | 0.7 | User comments on banned user's posts |
| Like | 0.6 | User likes banned user's posts |
| Indirect | 0.3-0.5 | Friend of friend |

## Testing

### Run Tests

```bash
cd backend/ML
python test_trust_propagation.py
```

### Test Scenarios

1. **Basic Propagation** - Verify penalties propagate correctly
2. **Follower Impact** - Followers receive higher penalties
3. **Distance Decay** - Penalties decrease with distance
4. **Trust Recovery** - Scores recover over time

### Manual Testing

```bash
# Start API
python trust_propagation_api.py

# In another terminal, test with curl
curl -X POST http://localhost:8006/build-graph \
  -H "Content-Type: application/json" \
  -d @test_data.json

curl -X POST http://localhost:8006/propagate-ban \
  -H "Content-Type: application/json" \
  -d '{"banned_user_id": "user1", "max_hops": 2, "base_penalty": 0.15}'
```

## Benefits

1. **Network Accountability** - Users are incentivized to be careful about who they follow
2. **Viral Harm Prevention** - Reduces spread of harmful content through social networks
3. **Graduated Response** - Penalties decrease with distance (fair to indirect connections)
4. **Recovery Mechanism** - Trust scores can recover over time
5. **Transparent** - Users can see their trust scores and understand why they changed

## Monitoring

### Key Metrics

- Number of users affected per ban
- Average penalty applied
- Distribution by distance
- Trust score recovery rates
- False positive rate (users unfairly penalized)

### Logs

```csharp
_logger.LogInformation($"Trust propagation completed. Affected {result.AffectedUserCount} users");
_logger.LogWarning($"User {userId} trust score dropped to {newScore}");
```

## Future Enhancements

1. **Weighted by Recency** - Recent interactions have higher weight
2. **Content-Specific** - Different penalties for different violation types
3. **Appeal System** - Users can appeal trust score penalties
4. **Positive Propagation** - Good behavior also propagates (slower)
5. **ML-Based Weights** - Learn optimal weights from data

## Troubleshooting

### API Not Starting

```bash
# Check if port 8006 is in use
netstat -ano | findstr :8006

# Install dependencies
pip install fastapi uvicorn pydantic
```

### Trust Scores Not Updating

1. Check API is running: http://localhost:8006
2. Verify MongoDB connection
3. Check logs for errors
4. Ensure TrustPropagationService is registered in DI

### Penalties Too High/Low

Adjust parameters in appsettings.json:
```json
{
  "TrustPropagation": {
    "BasePenalty": 0.10,
    "MaxHops": 1,
    "RecoveryRate": 0.02
  }
}
```

## References

- Graph Neural Networks (GNN) for trust scoring
- Social network analysis
- Content moderation best practices
- CLIP + OCR multimodal detection system
