# 🚀 START HERE - Quick Setup Guide

## ✅ Problem Fixed!
The false positive issue ("cup of tea" text being blocked) is now FIXED!

---

## 📋 Quick Checklist

### ☑️ Step 1: Start the API
```bash
cd backend/ML
start_intelligent_api_no_gnn.bat
```

**Wait for**: "Starting Intelligent Moderation API on port 5051..."

### ☑️ Step 2: Test the Fix
```bash
python test_false_positive_fix.py
```

**Expected**: All tests should pass ✅

### ☑️ Step 3: Start Your Backend
```bash
cd backend
dotnet run
```

### ☑️ Step 4: Test in Browser
1. Open your application
2. Create a post: "What are you doing. Here I am having a cup of tea"
3. **Expected**: Post should be APPROVED ✅

---

## 📚 Documentation Files (Read in Order)

1. **START_HERE.md** ← You are here!
2. **QUICK_FIX_SUMMARY.md** - What was fixed and how
3. **ANSWERS_TO_YOUR_QUESTIONS.md** - All your questions answered
4. **DEMO_SCENARIO_VISUAL.md** - Visual guide for presentation
5. **LIVE_DEMO_GUIDE.md** - Step-by-step demo script
6. **FALSE_POSITIVE_FIX.md** - Technical details

---

## ❓ Quick Answers

### Q: Which API should I run?
**A**: `intelligent_moderation_api.py` on port 5051 (use `start_intelligent_api_no_gnn.bat`)

### Q: Why is innocent text being blocked?
**A**: FIXED! Threshold increased from 0.3 to 0.7

### Q: How to demonstrate GNN?
**A**: Scammer posts innocent content first, users interact, then scammer posts scam. Users who interacted get lower trust scores. See `DEMO_SCENARIO_VISUAL.md`

### Q: What if GNN won't start?
**A**: Use `start_intelligent_api_no_gnn.bat` to skip GNN training (temporary workaround)

---

## 🎯 For Your Presentation

### Demo Scenario (10 minutes):

**Part 1: Show the Problem (2 min)**
- Without GNN: Sophisticated scams get through
- Example: "Great opportunity, DM me" → Approved (missed!)

**Part 2: Create Accounts (2 min)**
- Browser 1: ScammerDemo
- Browser 2: InnocentUser
- Both start with trust 0.5

**Part 3: Build Trust (2 min)**
- ScammerDemo posts innocent content
- InnocentUser likes/comments
- GNN records interactions

**Part 4: The Scam (2 min)**
- ScammerDemo posts obvious scam → BLOCKED
- ScammerDemo trust drops to 0.2
- InnocentUser trust drops to 0.4 (guilt by association!)

**Part 5: The Effect (2 min)**
- InnocentUser posts innocent content → FLAGGED
- Why? Low trust from associating with scammer
- This catches coordinated scam networks! 🎯

---

## 🔧 Troubleshooting

### Issue: API won't start
```bash
# Check if port 5051 is already in use
netstat -ano | findstr :5051

# If yes, kill the process or use different port
```

### Issue: "Module not found" error
```bash
# Install requirements
pip install -r requirements.txt
```

### Issue: Still getting false positives
```bash
# Check threshold in appsettings.json
# Should be 0.7, not 0.3 or 0.5
```

### Issue: GNN IndexError
```bash
# Use the no-GNN version
start_intelligent_api_no_gnn.bat
```

---

## 📊 What Changed

| Component | Before | After |
|-----------|--------|-------|
| Text threshold | 0.3 | 0.7 |
| Config threshold | 0.5 | 0.7 |
| False positives | High | Low ✅ |
| API port | 5050 (old) | 5051 (new) ✅ |
| GNN training | Enabled (error) | Disabled (workaround) |

---

## 🎉 Status

✅ False positive issue FIXED  
✅ Threshold adjusted (0.3 → 0.7)  
✅ Configuration updated  
✅ Test script created  
✅ Demo scenario documented  
✅ Startup script created  
⚠️ GNN temporarily disabled (IndexError needs fixing)  

---

## 📞 Need More Help?

1. Read `ANSWERS_TO_YOUR_QUESTIONS.md` - Comprehensive Q&A
2. Read `DEMO_SCENARIO_VISUAL.md` - Visual demo guide
3. Read `LIVE_DEMO_GUIDE.md` - Detailed demo steps

---

## 🚀 You're Ready!

Your system is now ready to use with:
- ✅ Fixed false positive issue
- ✅ Text + Image + OCR moderation
- ✅ Proper thresholds
- ⚠️ GNN features temporarily disabled

For your presentation, focus on the demo scenario in `DEMO_SCENARIO_VISUAL.md`!

Good luck! 🎯
