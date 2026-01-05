# Harmful Content Detection System

ML-based content moderation using Logistic Regression + TF-IDF for detecting toxic comments and posts.

## Model Files

Pre-trained models are stored in `models/` directory:
- `toxic_classifier.pkl` - Logistic Regression classifier
- `tfidf_vectorizer.pkl` - TF-IDF vectorizer

**These files are committed to the repository and loaded at runtime.**

## Local Development

### First Time Setup
```bash
cd backend/ML
pip install -r requirements.txt
```

### Training the Model (only if you need to retrain)
```bash
python train_model.py
```
This will:
1. Load `Data/train.csv` dataset
2. Train the model
3. Save to `models/` directory
4. **Commit the updated .pkl files to git**

### Running the API
```bash
python content_moderation_api.py
```
The API runs on `http://localhost:5050`

## Deployment

The Dockerfile copies pre-trained models from the repository - no training happens during build.

### Updating the Model
1. Train locally: `python train_model.py`
2. Test locally: `python content_moderation_api.py`
3. Commit model files: `git add models/*.pkl && git commit -m "Update ML models"`
4. Push and redeploy

## API Endpoints

### Health Check
```
GET /health
```

### Single Prediction
```
POST /predict
Content-Type: application/json

{
    "text": "content to analyze",
    "threshold": 0.7
}
```

Response:
```json
{
    "is_harmful": true,
    "confidence": 0.95,
    "threshold": 0.7
}
```

### Batch Prediction
```
POST /predict/batch
Content-Type: application/json

{
    "texts": ["text1", "text2"],
    "threshold": 0.7
}
```

## Model Performance

- Accuracy: 93.2%
- Harmful content precision: 62%
- Harmful content recall: 86%
- Threshold: 0.7 (configurable)

## Configuration

In main app's `appsettings.json`:
```json
{
    "ContentModeration": {
        "ApiUrl": "http://localhost:5050",
        "Threshold": "0.7"
    }
}
```

For Render deployment, set environment variables:
```
ContentModeration__ApiUrl=https://your-ml-api.onrender.com
ContentModeration__Threshold=0.7
```
