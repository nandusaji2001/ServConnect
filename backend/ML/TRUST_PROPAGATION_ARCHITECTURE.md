# Trust Score Propagation - System Architecture

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Community Platform                           │
│                                                                   │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │   User Posts │───▶│  Multimodal  │───▶│  Ban System  │      │
│  │   Content    │    │  AI Detection│    │              │      │
│  └──────────────┘    └──────────────┘    └──────┬───────┘      │
│                                                   │              │
│                                                   ▼              │
│                                          ┌────────────────┐     │
│                                          │ Trust Score    │     │
│                                          │ Propagation    │     │
│                                          └────────┬───────┘     │
│                                                   │              │
│                                                   ▼              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
│  │  Followers   │◀───│ Social Graph │───▶│  Affected    │     │
│  │  Penalized   │    │  Traversal   │    │  Users       │     │
│  └──────────────┘    └──────────────┘    └──────────────┘     │
└─────────────────────────────────────────────────────────────────┘
```

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Backend (C#)                             │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              CommunityController.cs                       │   │
│  │  - Handles user posts                                     │   │
│  │  - Triggers content moderation                            │   │
│  │  - Initiates ban process                                  │   │
│  └────────────────────┬─────────────────────────────────────┘   │
│                       │                                           │
│                       ▼                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              CommunityService.cs                          │   │
│  │  - RecordViolationAsync()                                 │   │
│  │  - Bans user after 5 violations                           │   │
│  │  - Sets ShouldPropagateTrustPenalty = true               │   │
│  └────────────────────┬─────────────────────────────────────┘   │
│                       │                                           │
│                       ▼                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         TrustPropagationService.cs                        │   │
│  │  - BuildSocialGraphAsync()                                │   │
│  │  - PropagateBanPenaltyAsync()                             │   │
│  │  - UpdateUserTrustScoresAsync()                           │   │
│  └────────────────────┬─────────────────────────────────────┘   │
│                       │ HTTP POST                                │
└───────────────────────┼──────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Python ML Service                             │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │         trust_propagation_api.py (FastAPI)               │   │
│  │  Port: 8006                                               │   │
│  │  - POST /build-graph                                      │   │
│  │  - POST /propagate-ban                                    │   │
│  │  - POST /compute-recovery                                 │   │
│  └────────────────────┬─────────────────────────────────────┘   │
│                       │                                           │
│                       ▼                                           │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │       trust_propagation_service.py                        │   │
│  │  - TrustPropagationService class                          │   │
│  │  - build_social_graph()                                   │   │
│  │  - propagate_ban_penalty()                                │   │
│  │  - Graph traversal algorithm (BFS)                        │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                      MongoDB                                     │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              CommunityProfiles                            │   │
│  │  - UserTrustScore (updated)                               │   │
│  │  - ContentTrustScore (updated)                            │   │
│  │  - LastTrustScoreUpdate                                   │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              UserFollows                                  │   │
│  │  - FollowerId, FollowingId                                │   │
│  │  - Used to build social graph                             │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow Sequence

```
1. User Posts Harmful Content
   ↓
2. Multimodal AI Detection (CLIP + OCR + Text)
   ↓
3. Content Flagged as Harmful
   ↓
4. CommunityService.RecordViolationAsync()
   ↓
5. Violation Count Incremented
   ↓
6. Check: ViolationCount >= 5?
   ├─ No → Return (no ban)
   └─ Yes → Continue
       ↓
7. User Banned (IsBanned = true)
   ↓
8. BanResult.ShouldPropagateTrustPenalty = true
   ↓
9. TrustPropagationService.PropagateBanPenaltyAsync()
   ↓
10. Build Social Graph
    - Fetch followers from UserFollows
    - Fetch interactions (likes, comments)
    - Fetch current trust scores
    ↓
11. HTTP POST to Python API (/propagate-ban)
    ↓
12. Python: Graph Traversal (BFS)
    - Start from banned user
    - Visit neighbors up to maxHops distance
    - Calculate penalties based on:
      * Distance (closer = higher penalty)
      * Connection weight (follower > liker)
    ↓
13. Python: Compute New Trust Scores
    - UserTrustScore -= penalty
    - ContentTrustScore += penalty * 1.5
    ↓
14. Return Affected Users to C#
    ↓
15. Update MongoDB
    - Bulk update CommunityProfiles
    - Set new trust scores
    - Set LastTrustScoreUpdate
    ↓
16. Log Results
    - Number of affected users
    - Average penalty applied
    ↓
17. Future Posts from Affected Users
    - Face stricter moderation
    - Based on ContentTrustScore
```

## Graph Traversal Algorithm

```
Algorithm: Breadth-First Search (BFS) with Weighted Edges

Input:
  - banned_user_id: User who was banned
  - max_hops: Maximum distance to propagate (default: 2)
  - base_penalty: Base penalty amount (default: 0.15)

Output:
  - affected_users: Map of user_id → penalty info

Steps:
1. Initialize queue with (banned_user_id, distance=0)
2. Initialize visited set
3. While queue is not empty:
   a. Dequeue (current_user, distance)
   b. If visited or distance > max_hops: continue
   c. Mark as visited
   d. If current_user == banned_user: skip penalty, add neighbors
   e. Calculate penalty:
      penalty = base_penalty × connection_weight × (1 / distance^1.5)
   f. Apply penalty:
      new_user_trust = current_trust - penalty
      new_content_trust = current_trust + (penalty × 1.5)
   g. Store in affected_users
   h. Add neighbors to queue with distance+1
4. Return affected_users

Example:
  Banned: user1
  Graph: user1 → user2 (follower, weight=0.8)
         user2 → user3 (follower, weight=0.8)
  
  Iteration 1: user2
    distance = 1
    penalty = 0.15 × 0.8 × 1.0 = 0.12
    user_trust: 0.8 → 0.68
    content_trust: 0.5 → 0.68
  
  Iteration 2: user3
    distance = 2
    penalty = 0.15 × 0.8 × 0.35 = 0.042
    user_trust: 0.8 → 0.758
    content_trust: 0.5 → 0.563
```

## Trust Score Calculation

```
User Trust Score (0-1, higher = more trustworthy)
  Initial: 0.5 (neutral)
  After penalty: max(0.1, current - penalty)
  
  Example:
    Current: 0.8
    Penalty: 0.12
    New: max(0.1, 0.8 - 0.12) = 0.68

Content Trust Score (0-1, higher = stricter moderation)
  Initial: 0.5 (normal moderation)
  After penalty: min(0.9, current + penalty × 1.5)
  
  Example:
    Current: 0.5
    Penalty: 0.12
    New: min(0.9, 0.5 + 0.12 × 1.5) = 0.68
    
  Usage in moderation:
    base_threshold = 0.7
    adjusted_threshold = 0.7 × (1 - 0.68 × 0.3) = 0.557
    
    Content is flagged if toxicity > 0.557 (instead of 0.7)
    This is 20% stricter moderation
```

## Connection Weights

```
Relationship Type          Weight    Penalty Multiplier
─────────────────────────────────────────────────────────
Follower                   0.8       1.0x (full penalty)
Mutual Follow              0.8       1.0x
Commenter (frequent)       0.7       0.875x
Liker (frequent)           0.6       0.75x
Indirect Connection        0.3-0.5   0.375-0.625x
```

## Distance Decay

```
Distance    Factor    Effective Penalty (base=0.15, weight=0.8)
──────────────────────────────────────────────────────────────
1 hop       1.0       0.15 × 0.8 × 1.0 = 0.12 (12%)
2 hops      0.35      0.15 × 0.8 × 0.35 = 0.042 (4.2%)
3 hops      0.19      0.15 × 0.8 × 0.19 = 0.023 (2.3%)

Formula: distance_factor = 1 / (distance ^ 1.5)
```

## Recovery Over Time

```
Trust Recovery (optional feature)

Formula:
  recovery_amount = min(recovery_rate × days, max_recovery)
  new_user_trust = min(0.9, current + recovery_amount)
  new_content_trust = max(0.3, current - recovery_amount)

Example (recovery_rate = 0.01):
  Day 0:  user_trust=0.68, content_trust=0.68
  Day 7:  user_trust=0.75, content_trust=0.61
  Day 30: user_trust=0.80, content_trust=0.40
  Day 60: user_trust=0.80, content_trust=0.30 (capped)
```

## Integration Points

### 1. Ban Trigger
```csharp
// In CommunityService.cs
if (profile.CurrentViolationStreak >= 5)
{
    profile.IsBanned = true;
    result.ShouldPropagateTrustPenalty = true; // ← NEW
}
```

### 2. Propagation Call
```csharp
// In Controller
if (banResult.ShouldPropagateTrustPenalty)
{
    await _trustPropagation.PropagateBanPenaltyAsync(userId, 2, 0.15);
}
```

### 3. Moderation Adjustment
```csharp
// In ContentModerationService
var profile = await _community.GetProfileByUserIdAsync(userId);
double adjustedThreshold = baseThreshold * (1.0 - profile.ContentTrustScore * 0.3);
```

## Performance Considerations

### Graph Size
- Small community (<1000 users): Real-time propagation
- Medium community (1000-10000 users): <1 second
- Large community (>10000 users): Consider async job queue

### Optimization Strategies
1. **Limit max_hops**: Keep at 2 for most cases
2. **Cache social graph**: Rebuild periodically, not per ban
3. **Batch updates**: Use MongoDB bulk operations
4. **Async processing**: Queue propagation for large graphs

### Scalability
```
Users     Followers/User    Propagation Time
─────────────────────────────────────────────
1,000     50                ~50ms
10,000    100               ~200ms
100,000   200               ~1-2s (use queue)
1,000,000 500               ~10-30s (use queue)
```

## Monitoring Metrics

### Key Metrics to Track
1. **Affected users per ban**: Average and distribution
2. **Penalty distribution**: By distance and relationship
3. **Trust score distribution**: Histogram of all users
4. **False positive rate**: Users unfairly penalized
5. **Recovery rate**: How fast users recover trust

### Alerts
- Alert if >100 users affected by single ban
- Alert if average trust score drops below 0.4
- Alert if API response time >5 seconds

## Security Considerations

1. **Rate limiting**: Prevent abuse of propagation system
2. **Validation**: Verify banned user exists before propagation
3. **Authorization**: Only admins can trigger manual propagation
4. **Audit log**: Track all trust score changes
5. **Appeal system**: Allow users to contest penalties

## Future Enhancements

1. **Positive propagation**: Good behavior also spreads (slower)
2. **Weighted by recency**: Recent interactions matter more
3. **Content-specific**: Different penalties for different violations
4. **ML-based weights**: Learn optimal weights from data
5. **Reputation system**: Combine trust scores with other signals
