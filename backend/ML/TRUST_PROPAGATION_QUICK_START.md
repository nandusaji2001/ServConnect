# Trust Score Propagation - Quick Start Guide

## What This Does

When a user posts harmful content and gets banned, their followers and people who interact with them also get penalized with:
- **Lower user trust score** (they're less trustworthy)
- **Higher content trust score** (their posts face stricter moderation)

This creates accountability in your social network!

## 5-Minute Setup

### 1. Start the API

```bash
cd backend/ML
python trust_propagation_api.py
```

You should see:
```
Starting Trust Propagation API...
API will be available at: http://localhost:8006
Documentation at: http://localhost:8006/docs
```

### 2. Test It Works

Open http://localhost:8006 in your browser. You should see:
```json
{
  "service": "Trust Propagation API",
  "status": "running",
  "version": "1.0.0"
}
```

### 3. Run Tests

```bash
python test_trust_propagation.py
```

You should see all tests pass with output showing how penalties propagate.

### 4. Add to Your Code

In your controller where you ban users:

```csharp
// After banning a user
var banResult = await _community.RecordViolationAsync(userId, content, contentType, toxicityScore, reason);

if (banResult.WasBanned)
{
    // NEW: Propagate trust penalties
    var propagationResult = await _trustPropagation.PropagateBanPenaltyAsync(
        bannedUserId: userId,
        maxHops: 2,
        basePenalty: 0.15
    );
    
    _logger.LogInformation($"Affected {propagationResult.AffectedUserCount} users");
}
```

## How It Works (Simple Explanation)

```
User A posts hate speech → Gets banned
    ↓
User B (follows A) → Trust drops from 0.8 to 0.68
    ↓
User C (follows B) → Trust drops from 0.8 to 0.76
```

The further away you are from the banned user, the smaller the penalty.

## Key Numbers

- **Direct follower**: -12% trust penalty
- **Friend of friend**: -4% trust penalty
- **Content trust increases**: Posts face 20-30% stricter moderation

## Configuration

### Mild Penalties (First-time offenders)
```csharp
await _trustPropagation.PropagateBanPenaltyAsync(userId, maxHops: 1, basePenalty: 0.10);
```

### Standard Penalties (Recommended)
```csharp
await _trustPropagation.PropagateBanPenaltyAsync(userId, maxHops: 2, basePenalty: 0.15);
```

### Severe Penalties (Repeat offenders)
```csharp
await _trustPropagation.PropagateBanPenaltyAsync(userId, maxHops: 3, basePenalty: 0.25);
```

## Check Trust Scores

Query MongoDB:
```javascript
db.CommunityProfiles.find({}, {
  Username: 1,
  UserTrustScore: 1,
  ContentTrustScore: 1
}).sort({ UserTrustScore: 1 })
```

## Use Trust Scores in Moderation

```csharp
var profile = await _community.GetProfileByUserIdAsync(userId);

// Adjust threshold based on content trust score
double baseThreshold = 0.7;
double adjustedThreshold = baseThreshold * (1.0 - profile.ContentTrustScore * 0.3);

// Users with high content trust face stricter moderation
if (toxicityScore > adjustedThreshold)
{
    // Flag content
}
```

## Troubleshooting

### API won't start
```bash
pip install fastapi uvicorn pydantic
```

### No users affected
- Check API is running: http://localhost:8006
- Verify users have followers in database
- Check logs for errors

### Penalties too high/low
Adjust `basePenalty` parameter (0.05 to 0.30)

## What's Next?

1. Monitor how many users are affected per ban
2. Adjust penalties based on your community
3. Add UI to show trust scores to users
4. Implement trust recovery over time

## Full Documentation

See `TRUST_PROPAGATION_README.md` for complete details.
