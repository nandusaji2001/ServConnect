# Quick Demo Reference Card

## 🚀 Start APIs (1 command)

```bash
cd backend/ML
START_ALL_MODERATION_APIS.bat
```

This starts:
- Intelligent Moderation API (port 8002)
- Trust Propagation API (port 8006)

## 📝 Perfect Demo Content

### Content That Shows the Difference

**Post this as both normal user and follower of banned user:**

```
I really disagree with this policy. The government officials 
are making terrible decisions that hurt our community. 
This is frustrating and needs to change immediately!
```

**Expected Results:**
- Normal User: ✅ **APPROVED** (toxicity 0.65 < threshold 0.70)
- Follower: ❌ **FLAGGED** (toxicity 0.65 > threshold 0.557)

### Alternative Demo Content

**Option 2:**
```
This service is absolutely terrible. I'm so disappointed 
with how things are being handled. What a waste of time.
```

**Option 3:**
```
I strongly oppose this decision. The leadership has failed 
to consider the consequences. This is wrong.
```

## 🎯 Demo Steps (5 minutes)

### 1. Setup (30 seconds)
- Create UserA (will be banned)
- Create UserB (follows UserA)
- Create UserC (normal user)
- Make UserB follow UserA

### 2. Ban UserA (2 minutes)
Post 5 harmful messages as UserA:
1. "I hate everyone in this group. You're all idiots."
2. "Violence is the only answer. Time to take action!"
3. "All [group] are terrible and should be removed."
4. "I hope something bad happens to them."
5. "Extreme hate speech content."

→ UserA gets banned after 5th message

### 3. Show Trust Changes (1 minute)
```
UserA: BANNED
UserB: Trust 0.8→0.68 (-15%), ContentTrust 0.5→0.68 (+36%)
UserC: Trust 0.8 (unchanged), ContentTrust 0.5 (unchanged)
```

### 4. Show Different Moderation (1.5 minutes)
- Post demo content as UserC → ✅ APPROVED
- Post same content as UserB → ❌ FLAGGED
- Show: "Same content, different results!"

## 🧪 Test Before Demo

```bash
# Test if APIs are running
curl http://localhost:8002/
curl http://localhost:8006/

# Find perfect demo content
python test_demo_content.py

# Test custom content
python test_demo_content.py "Your text here"
```

## 📊 Key Numbers to Mention

- **Follower penalty**: 15% trust reduction
- **Moderation strictness**: 20% stricter for followers
- **Distance decay**: Friends of friends get only 5% penalty
- **Recovery time**: 30 days to full recovery

## 💡 Key Talking Points

1. **Problem**: Followers of harmful users face no consequences
2. **Solution**: Trust score propagation through social graph
3. **Fair**: Penalties decrease with distance
4. **Recoverable**: Trust scores improve over time
5. **Automated**: No manual intervention needed

## 🎨 Visual Comparison

```
┌─────────────────┬──────────┬───────────┬──────────┐
│ User Type       │ Trust    │ Threshold │ Result   │
├─────────────────┼──────────┼───────────┼──────────┤
│ Normal User     │ 0.80     │ 0.700     │ ✅ Pass  │
│ Follower        │ 0.68     │ 0.557     │ ❌ Flag  │
└─────────────────┴──────────┴───────────┴──────────┘

Same content (toxicity 0.65), different results!
```

## ⚠️ Troubleshooting

**APIs won't start:**
```bash
pip install fastapi uvicorn pydantic
```

**Content not flagged:**
- Use test_demo_content.py to find better content
- Make content more critical/negative

**No trust propagation:**
- Verify UserB actually follows UserA
- Check both APIs are running
- Look at logs for errors

## 📱 Quick Commands

```bash
# Start everything
START_ALL_MODERATION_APIS.bat

# Test content
python test_demo_content.py

# Run full tests
python test_trust_propagation.py

# Check MongoDB
db.CommunityProfiles.find({Username: "Follower"})
```

## 🎯 Success Criteria

Demo is successful if you show:
1. ✅ UserA gets banned after 5 violations
2. ✅ UserB's trust score drops (0.8 → 0.68)
3. ✅ UserC's trust score unchanged (0.8)
4. ✅ Same content: UserC approved, UserB flagged
5. ✅ Audience understands network accountability

## 📞 Emergency Backup

If live demo fails, show:
1. Test results from `test_trust_propagation.py`
2. Screenshots of trust score changes
3. Video recording of working demo
4. Slides with expected results

## 🎉 Closing Statement

"This system creates network accountability where harmful behavior impacts not just the violator, but their social connections. Users now think twice about who they follow, making our community safer for everyone."

---

**Print this page and keep it handy during your demo!**
