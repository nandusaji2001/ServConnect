# start_all_apis.bat - Updated ✅

## What Changed

The `start_all_apis.bat` script has been updated to include the new **Intelligent Moderation API**.

## APIs Started

When you run `start_all_apis.bat`, it now starts **6 APIs** (previously 5):

1. **Content Moderation API (Legacy)** - Port 5050
   - Text-only moderation
   - TF-IDF + Logistic Regression
   - Fast and lightweight

2. **Intelligent Moderation API (NEW)** - Port 5051 ⭐
   - Multimodal AI (Text + Image)
   - Graph Neural Networks (User Trust)
   - BERT/CLIP/GNN architecture
   - 4-level recommendations

3. **Elder Wellness API** - Port 5002
   - Diet recommendations
   - Health predictions

4. **Multimodal Item Matching API** - Port 5003
   - CLIP + GNN for Lost & Found
   - Image + text matching

5. **ID Verification API** - Port 5004
   - OCR-based verification
   - EasyOCR

6. **Depression Prediction API** - Port 5007
   - Mental health assessment
   - XGBoost classifier

## How to Use

### Start All APIs
```bash
cd backend/ML
start_all_apis.bat
```

This will:
1. Create/activate virtual environment
2. Install dependencies
3. Train models if needed
4. Start all 6 APIs in separate windows

### Test Intelligent Moderation API
```bash
# Quick test
python test_intelligent_api.py

# Full demo
python demo_intelligent_moderation.py

# Compare with legacy
python compare_moderation_systems.py
```

## API Endpoints

### Intelligent Moderation API (Port 5051)

**Health Check:**
```bash
curl http://localhost:5051/health
```

**Analyze Text:**
```bash
curl -X POST http://localhost:5051/analyze/text \
  -H "Content-Type: application/json" \
  -d '{"text": "Test message"}'
```

**Analyze Content (Comprehensive):**
```bash
curl -X POST http://localhost:5051/analyze/content \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Check out this photo!",
    "user_id": "user1",
    "post_id": "post1"
  }'
```

**Get User Trust Score:**
```bash
curl -X POST http://localhost:5051/gnn/trust \
  -H "Content-Type: application/json" \
  -d '{"user_id": "user1"}'
```

**Legacy Endpoint (Backward Compatible):**
```bash
curl -X POST http://localhost:5051/predict \
  -H "Content-Type: application/json" \
  -d '{"text": "Test", "threshold": 0.5}'
```

## Window Layout

When you run `start_all_apis.bat`, you'll see 6 separate command windows:

```
┌─────────────────────────────────────────────────────────┐
│ Content Moderation API - Port 5050 [Legacy]            │
├─────────────────────────────────────────────────────────┤
│ Intelligent Moderation API - Port 5051 [BERT+CLIP+GNN] │
├─────────────────────────────────────────────────────────┤
│ Elder Wellness API - Port 5002                          │
├─────────────────────────────────────────────────────────┤
│ Multimodal Item Matching API - Port 5003               │
├─────────────────────────────────────────────────────────┤
│ ID Verification API - Port 5004                         │
├─────────────────────────────────────────────────────────┤
│ Depression Prediction API - Port 5007                   │
└─────────────────────────────────────────────────────────┘
```

Each window shows the API logs and can be closed independently.

## Integration with C# Backend

### Update appsettings.json

```json
{
  "ContentModeration": {
    "LegacyApiUrl": "http://localhost:5050",
    "IntelligentApiUrl": "http://localhost:5051",
    "UseIntelligent": true,
    "Threshold": 0.5
  }
}
```

### Use in Controllers

```csharp
// For simple text-only moderation (fast)
var result = await _moderationService.AnalyzeTextAsync(text);

// For comprehensive moderation (text + image + user behavior)
var result = await _moderationService.AnalyzeContentAsync(
    text: post.Caption,
    imageData: imageBytes,
    userId: currentUserId,
    postId: post.Id
);

// Handle recommendation
switch (result.Recommendation)
{
    case "block":
        return BadRequest("Content violates guidelines");
    case "review":
        post.IsHidden = true; // Send for manual review
        break;
    case "flag":
        post.IsFlagged = true; // Monitor
        break;
    case "approve":
        // All good
        break;
}
```

## Troubleshooting

### Issue: API not starting
**Solution:** Check if port is already in use
```bash
netstat -ano | findstr :5051
```

### Issue: Models not loading
**Solution:** Train models first
```bash
python train_model.py
```

### Issue: Import errors
**Solution:** Install dependencies
```bash
pip install -r requirements.txt
```

### Issue: Slow startup
**Solution:** First startup downloads models (~1GB), subsequent starts are faster

## Performance Tips

1. **Use Legacy for high-volume text-only**: Port 5050 is 4x faster
2. **Use Intelligent for critical content**: Port 5051 is more accurate
3. **Hybrid approach**: Legacy for initial filter, Intelligent for flagged content
4. **GPU acceleration**: Set CUDA_VISIBLE_DEVICES if GPU available

## Documentation

- `INTELLIGENT_MODERATION_README.md` - Complete guide
- `INTEGRATION_GUIDE.md` - C# integration steps
- `SYSTEM_COMPARISON.md` - Legacy vs Intelligent comparison
- `UPGRADE_COMPLETE.md` - Quick start guide
- `ml_algorithms_used.txt` - All algorithms explained

## Next Steps

1. ✅ Run `start_all_apis.bat`
2. ✅ Test with `test_intelligent_api.py`
3. ✅ Try demo with `demo_intelligent_moderation.py`
4. ✅ Integrate with C# backend
5. ✅ Monitor and tune weights

## Summary

The `start_all_apis.bat` script now includes the **Intelligent Moderation API** on port 5051, providing advanced multimodal content moderation with:
- Text analysis (BERT/TF-IDF)
- Image analysis (CLIP)
- User trust scoring (GNN)
- Context-aware decisions
- Explainable AI

Both legacy (5050) and intelligent (5051) APIs run simultaneously for easy A/B testing and gradual migration.
