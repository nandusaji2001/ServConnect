# Intelligent Content Moderation System

## Overview

Research-level intelligent moderation system combining:
- **Multimodal AI**: Text (BERT/TF-IDF) + Image (CLIP) analysis
- **Graph Neural Networks**: User behavior and trust scoring
- **Hybrid Decision Logic**: Weighted combination of all signals

## Architecture

### Components

1. **text_model.py** - Text toxicity detection
   - Legacy: TF-IDF + Logistic Regression
   - Modern: BERT/DistilBERT transformers
   - Output: toxicity_score (0-1)

2. **clip_service.py** - Image content analysis
   - CLIP (openai/clip-vit-base-patch32)
   - Detects harmful visual content
   - Image-text consistency scoring
   - Output: image_risk_score (0-1)

3. **graph_builder.py** - Community graph construction
   - Nodes: Users, Posts
   - Edges: posted, liked, commented, reported
   - Features: account age, post count, reports, etc.

4. **gnn_model.py** - Graph Neural Network
   - Architecture: GraphSAGE or GAT
   - Computes user trust scores
   - Computes post risk scores
   - Output: trust_score (0-1), risk_score (0-1)

5. **moderation_service.py** - Unified moderation logic
   - Combines all signals
   - Final risk = α×text + β×image + γ×(1-trust)
   - Decision: block, review, flag, approve

6. **intelligent_moderation_api.py** - Flask API
   - RESTful endpoints
   - Backward compatible with legacy system


## Installation

```bash
cd backend/ML
pip install -r requirements.txt
```

### PyTorch Geometric Installation

```bash
# For CPU
pip install torch-scatter torch-sparse -f https://data.pyg.org/whl/torch-2.0.0+cpu.html

# For CUDA 11.8
pip install torch-scatter torch-sparse -f https://data.pyg.org/whl/torch-2.0.0+cu118.html
```

## Quick Start

### 1. Run Demo

```bash
python demo_intelligent_moderation.py
```

This demonstrates:
- Text toxicity detection
- Image content analysis with CLIP
- GNN user trust scoring
- Comprehensive multimodal analysis
- Weight tuning

### 2. Start API Server

```bash
python intelligent_moderation_api.py
```

Server runs on `http://localhost:5051`

### 3. Test API

```bash
# Text analysis
curl -X POST http://localhost:5051/analyze/text \
  -H "Content-Type: application/json" \
  -d '{"text": "This is a test message"}'

# Comprehensive analysis
curl -X POST http://localhost:5051/analyze/content \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Check out this photo!",
    "user_id": "user1",
    "post_id": "post1"
  }'
```

## API Endpoints

### Text Analysis
`POST /analyze/text`
```json
{
  "text": "content to analyze"
}
```

### Image Analysis
`POST /analyze/image`
```json
{
  "image": "base64_encoded_image",
  "caption": "optional caption"
}
```

### Comprehensive Analysis
`POST /analyze/content`
```json
{
  "text": "post caption",
  "image": "base64_encoded_image",
  "user_id": "user123",
  "post_id": "post456"
}
```

Response:
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

### GNN Trust Score
`POST /gnn/trust`
```json
{
  "user_id": "user123"
}
```

### Update Weights
`POST /config/weights`
```json
{
  "alpha": 0.4,
  "beta": 0.3,
  "gamma": 0.3
}
```

## Configuration

### Model Selection

**Text Model:**
- `legacy`: TF-IDF + Logistic Regression (fast, lightweight)
- `transformer`: BERT/DistilBERT (accurate, slower)

**GNN Architecture:**
- `GraphSAGE`: Fast, scalable (recommended)
- `GAT`: Attention-based, more expressive

### Scoring Weights

- **alpha** (text): Weight for text toxicity (default: 0.4)
- **beta** (image): Weight for image risk (default: 0.3)
- **gamma** (trust): Weight for user behavior (default: 0.3)

Adjust based on your priorities:
- Text-focused: α=0.7, β=0.2, γ=0.1
- Image-focused: α=0.2, β=0.7, γ=0.1
- Trust-focused: α=0.2, β=0.2, γ=0.6

## Integration with Backend

### Update C# Service

```csharp
public async Task<ModerationResult> AnalyzeContentAsync(
    string text, 
    byte[] imageData, 
    string userId, 
    string postId)
{
    var request = new
    {
        text = text,
        image = imageData != null ? Convert.ToBase64String(imageData) : null,
        user_id = userId,
        post_id = postId
    };
    
    var response = await _httpClient.PostAsJsonAsync(
        "http://localhost:5051/analyze/content", 
        request
    );
    
    return await response.Content.ReadFromJsonAsync<ModerationResult>();
}
```

## Training GNN

### With Real Data

```python
from graph_builder import CommunityGraphBuilder
from moderation_service import IntelligentModerationService

# Build graph from database
builder = CommunityGraphBuilder()

for user in database.get_users():
    builder.add_user(
        user.id,
        user.created_at,
        posts_count=user.posts.count(),
        harmful_posts=user.flagged_posts.count(),
        reports_received=user.reports.count()
    )

for post in database.get_posts():
    builder.add_post(
        post.id,
        post.user_id,
        post.created_at,
        likes_count=post.likes.count(),
        reports_count=post.reports.count(),
        is_flagged=post.is_flagged
    )

# Initialize and train
service = IntelligentModerationService()
service.initialize_gnn(builder, train=True, epochs=100)
```

## Performance

### Text Model
- Legacy: ~1ms per text
- Transformer: ~50ms per text

### Image Analysis (CLIP)
- ~100ms per image (CPU)
- ~20ms per image (GPU)

### GNN Inference
- ~10ms for trust score lookup
- Graph build: one-time cost

## Upgrading from Legacy

The system is backward compatible:

1. Legacy endpoint `/predict` still works
2. Gradual migration: use new endpoints for new features
3. A/B testing: compare legacy vs intelligent moderation

## Next Steps

1. **Replace Text Model**: Switch to BERT for better accuracy
2. **Train on Labeled Data**: Use real moderation decisions
3. **Tune Thresholds**: Adjust based on false positive/negative rates
4. **Monitor Performance**: Track precision, recall, F1 score
5. **Continuous Learning**: Retrain GNN periodically

## Research References

- CLIP: https://github.com/openai/CLIP
- GraphSAGE: https://arxiv.org/abs/1706.02216
- GAT: https://arxiv.org/abs/1710.10903
- Toxic Comment Classification: https://www.kaggle.com/c/jigsaw-toxic-comment-classification-challenge
