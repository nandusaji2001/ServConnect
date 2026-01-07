# ML Services for ServConnect

This directory contains two ML-powered APIs:
1. **Content Moderation API** - Harmful content detection for Community module
2. **Elder Wellness API** - Diet and health recommendations for Elder Care module

---

## 1. Harmful Content Detection System

ML-based content moderation using Logistic Regression + TF-IDF for detecting toxic comments and posts.

### Model Files

Pre-trained models are stored in `models/` directory:
- `toxic_classifier.pkl` - Logistic Regression classifier
- `tfidf_vectorizer.pkl` - TF-IDF vectorizer

### Running the Content Moderation API

```bash
# Windows
start_moderation_api.bat

# Or manually
cd backend/ML
pip install -r requirements.txt
python content_moderation_api.py
```
The API runs on `http://localhost:5050`

### Training (if needed)
```bash
python train_model.py
```

---

## 2. Elder Wellness Prediction System

ML-based health recommendations using Random Forest Classifier for:
- Diet Recommendations
- Diet Plan suggestions
- Heart Risk assessment

### Model Files

Trained models are stored in `models/` directory:
- `wellness_models.pkl` - Contains all 3 Random Forest models + encoders

### Dataset

Located at `Data/elder_diet_ml_ready_dataset_v3.csv`

Features used:
- age, gender, bmi
- systolic_bp, diastolic_bp
- cholesterol, triglycerides
- family_history_t2d, family_history_cvd
- sleep_hours, sleep_quality
- stress_level, physical_activity_level

### Running the Wellness API

**Option 1: Using batch file (Recommended)**
```bash
# This will train models if not found, then start the API
start_wellness_api.bat
```

**Option 2: Manual steps**
```bash
cd backend/ML

# Create virtual environment (first time)
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Train models (first time or to retrain)
python train_wellness_model.py

# Start the API
python elder_wellness_api.py
```

The API runs on `http://localhost:5002`

### Training Only
```bash
# Windows
train_wellness_model.bat

# Or manually
python train_wellness_model.py
```

### API Endpoints

#### Health Check
```
GET /health
```

#### Get Predictions
```
POST /predict
Content-Type: application/json

{
    "age": 65,
    "gender": "Male",
    "bmi": 26.5,
    "systolic_bp": 130,
    "diastolic_bp": 85,
    "cholesterol": 210,
    "triglycerides": 150,
    "family_history_t2d": 1,
    "family_history_cvd": 0,
    "sleep_hours": 7,
    "sleep_quality": "Good",
    "stress_level": 5,
    "physical_activity_level": "Lightly Active",
    "diet_preference": "vegetarian",
    "food_allergies": "nuts"
}
```

Response includes:
- Predicted diet recommendation
- Predicted diet plan
- Heart risk level (low/moderate/high)
- Detailed meal plans (breakfast, lunch, dinner, snacks)
- Exercise recommendations based on heart risk
- Dietary and lifestyle tips

#### Retrain Models
```
POST /train
```

---

## Configuration

In `appsettings.json`:
```json
{
    "ContentModeration": {
        "ApiUrl": "http://localhost:5050",
        "Threshold": "0.7"
    },
    "WellnessApi": {
        "BaseUrl": "http://localhost:5002"
    }
}
```

## Docker Deployment

Both APIs are included in docker-compose.yml:
- `ml-api` - Content Moderation (port 5050)
- `wellness-api` - Elder Wellness (port 5002)

```bash
docker-compose up -d
```

## Quick Start (Local Development)

1. **Start Content Moderation API** (for Community module):
   ```bash
   cd backend/ML
   start_moderation_api.bat
   ```

2. **Start Wellness API** (for Elder Care module):
   ```bash
   cd backend/ML
   start_wellness_api.bat
   ```

3. **Start the main .NET application**:
   ```bash
   cd backend
   dotnet run
   ```
