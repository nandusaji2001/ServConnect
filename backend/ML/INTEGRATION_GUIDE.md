# Integration Guide: Intelligent Moderation System

## Quick Start

### 1. Install Dependencies

```bash
cd backend/ML
pip install -r requirements.txt
```

### 2. Start API Server

```bash
# Windows
start_intelligent_moderation_api.bat

# Linux/Mac
python intelligent_moderation_api.py
```

Server runs on `http://localhost:5051`

### 3. Test API

```bash
python demo_intelligent_moderation.py
```

## C# Backend Integration

### Update ContentModerationService.cs

```csharp
public class IntelligentModerationResult
{
    [JsonPropertyName("text_toxicity_score")]
    public double TextToxicityScore { get; set; }
    
    [JsonPropertyName("image_risk_score")]
    public double ImageRiskScore { get; set; }
    
    [JsonPropertyName("user_trust_score")]
    public double UserTrustScore { get; set; }
    
    [JsonPropertyName("post_risk_score")]
    public double PostRiskScore { get; set; }
    
    [JsonPropertyName("final_risk_score")]
    public double FinalRiskScore { get; set; }
    
    [JsonPropertyName("is_harmful")]
    public bool IsHarmful { get; set; }
    
    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; }
}

public async Task<IntelligentModerationResult> AnalyzeContentAsync(
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
    
    var json = JsonSerializer.Serialize(request);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    
    var response = await _httpClient.PostAsync(
        "http://localhost:5051/analyze/content", 
        content
    );
    
    if (response.IsSuccessStatusCode)
    {
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IntelligentModerationResult>(
            responseJson, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
    }
    
    return null;
}
```

### Update CommunityController.cs

```csharp
[HttpPost]
public async Task<IActionResult> CreatePost(CreatePostViewModel model)
{
    // Get image bytes if uploaded
    byte[] imageBytes = null;
    if (model.Image != null)
    {
        using (var ms = new MemoryStream())
        {
            await model.Image.CopyToAsync(ms);
            imageBytes = ms.ToArray();
        }
    }
    
    // Analyze content
    var moderationResult = await _moderationService.AnalyzeContentAsync(
        model.Caption,
        imageBytes,
        User.FindFirstValue(ClaimTypes.NameIdentifier),
        null  // Post ID not yet created
    );
    
    // Handle recommendation
    if (moderationResult.Recommendation == "block")
    {
        return Json(new { 
            success = false, 
            message = "Content violates community guidelines" 
        });
    }
    
    // Create post
    var post = new CommunityPost
    {
        Caption = model.Caption,
        ImageUrl = await SaveImage(imageBytes),
        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        IsHidden = moderationResult.Recommendation == "review",
        IsFlagged = moderationResult.Recommendation == "flag",
        CreatedAt = DateTime.UtcNow
    };
    
    await _communityService.CreatePostAsync(post);
    
    return Json(new { success = true });
}
```

## Configuration

### appsettings.json

```json
{
  "ContentModeration": {
    "ApiUrl": "http://localhost:5051",
    "Threshold": 0.5,
    "Weights": {
      "Alpha": 0.4,
      "Beta": 0.3,
      "Gamma": 0.3
    }
  }
}
```

## Deployment

### Docker Compose

```yaml
services:
  intelligent-moderation:
    build:
      context: ./backend/ML
      dockerfile: Dockerfile.intelligent
    ports:
      - "5051:5051"
    environment:
      - MODEL_TYPE=legacy
      - USE_GAT=false
    volumes:
      - ./backend/ML/models:/app/models
```

### Dockerfile.intelligent

```dockerfile
FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 5051

CMD ["python", "intelligent_moderation_api.py"]
```

## Performance Optimization

### 1. Model Caching
- Models loaded once at startup
- Reused for all requests
- ~200ms per request

### 2. Batch Processing
```csharp
var results = await _moderationService.AnalyzeBatchAsync(posts);
```

### 3. Async Processing
- Queue posts for moderation
- Process in background
- Return immediate response

### 4. Fallback Strategy
```csharp
try
{
    var result = await _intelligentModeration.AnalyzeAsync(...);
}
catch
{
    // Fallback to legacy system
    var result = await _legacyModeration.AnalyzeAsync(...);
}
```

## Monitoring

### Health Check
```bash
curl http://localhost:5051/health
```

### Metrics to Track
- Average response time
- False positive rate
- False negative rate
- User trust score distribution
- Blocked/flagged content ratio

## Troubleshooting

### Issue: Models not loading
**Solution:** Ensure models exist in `backend/ML/models/`
```bash
python train_model.py
```

### Issue: Slow inference
**Solution:** Use GPU or reduce batch size
```python
service = IntelligentModerationService(
    text_model_type="legacy"  # Faster than transformer
)
```

### Issue: High memory usage
**Solution:** Restart service periodically or use model quantization

## Next Steps

1. Train GNN on real community data
2. Fine-tune BERT on your specific content
3. Adjust weights based on A/B testing
4. Implement continuous learning pipeline
5. Add explainability dashboard
