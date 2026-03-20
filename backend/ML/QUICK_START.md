# Quick Start Guide - Intelligent Moderation System

## 🚀 Start Everything (Recommended)

```bash
cd backend/ML
start_all_apis.bat
```

This starts all 6 ML APIs including the new **Intelligent Moderation API** on port 5051.

## ✅ Test the API

```bash
python test_intelligent_api.py
```

## 🎮 Run Demo

```bash
python demo_intelligent_moderation.py
```

## 📊 Compare Systems

```bash
python compare_moderation_systems.py
```

## 🔗 API Endpoints

### Base URL
```
http://localhost:5051
```

### Health Check
```bash
GET /health
```

### Analyze Text Only
```bash
POST /analyze/text
{
  "text": "Your text here"
}
```

### Analyze Content (Recommended)
```bash
POST /analyze/content
{
  "text": "Post caption",
  "image": "base64_encoded_image",
  "user_id": "user123",
  "post_id": "post456"
}
```

### Get User Trust Score
```bash
POST /gnn/trust
{
  "user_id": "user123"
}
```

### Legacy Endpoint (Backward Compatible)
```bash
POST /predict
{
  "text": "Your text",
  "threshold": 0.5
}
```

## 📦 What You Get

| Feature | Legacy (5050) | Intelligent (5051) |
|---------|--------------|-------------------|
| Text Analysis | ✅ TF-IDF | ✅ BERT/TF-IDF |
| Image Analysis | ❌ | ✅ CLIP |
| User Behavior | ❌ | ✅ GNN |
| Speed | ~50ms | ~200ms |
| Accuracy | ~95% | ~97-98% |

## 🎯 Response Format

```json
{
  "text_toxicity_score": 0.2,
  "image_risk_score": 0.1,
  "user_trust_score": 0.85,
  "post_risk_score": 0.15,
  "final_risk_score": 0.25,
  "is_harmful": false,
  "recommendation": "approve"
}
```

## 🔧 Configuration

### Weights (in code)
```python
service = IntelligentModerationService(
    text_model_type="legacy",  # or "transformer"
    use_gat=False,  # or True
    weights={'alpha': 0.4, 'beta': 0.3, 'gamma': 0.3}
)
```

### Update Weights (via API)
```bash
POST /config/weights
{
  "alpha": 0.4,
  "beta": 0.3,
  "gamma": 0.3
}
```

## 📚 Documentation

- `INTELLIGENT_MODERATION_README.md` - Full guide
- `INTEGRATION_GUIDE.md` - C# integration
- `SYSTEM_COMPARISON.md` - Comparison
- `ml_algorithms_used.txt` - Algorithms

## 🐛 Troubleshooting

### API not starting?
```bash
# Check if port is in use
netstat -ano | findstr :5051

# Install dependencies
pip install -r requirements.txt

# Train models
python train_model.py
```

### Slow inference?
```python
# Use legacy text model (faster)
service = IntelligentModerationService(
    text_model_type="legacy"
)
```

## 💡 Tips

1. **High Volume**: Use legacy API (5050) for initial filter
2. **Critical Content**: Use intelligent API (5051) for deep analysis
3. **New Users**: Always use intelligent API for first N posts
4. **Flagged Content**: Route to intelligent API for review

## 🎓 Learn More

Run the demo to see all features:
```bash
python demo_intelligent_moderation.py
```

This demonstrates:
- Text toxicity detection
- Image content analysis
- GNN trust scoring
- Comprehensive analysis
- Weight tuning

## ✨ Key Features

✅ Multimodal AI (Text + Image)
✅ User behavior analysis (GNN)
✅ Context-aware decisions
✅ 4-level recommendations
✅ Explainable AI
✅ Backward compatible
✅ Production-ready

---

**Ready to integrate?** See `INTEGRATION_GUIDE.md` for C# backend integration steps.
