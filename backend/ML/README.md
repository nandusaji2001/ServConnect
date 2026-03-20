# ML Services for ServConnect

This directory contains multiple ML-powered APIs:
1. **Content Moderation API (Legacy)** - Text-only harmful content detection
2. **Intelligent Moderation API (NEW)** - Multimodal AI + GNN for advanced moderation
3. **Elder Wellness API** - Diet and health recommendations for Elder Care module
4. **Multimodal Item Matching API** - CLIP + GNN for Lost & Found module
5. **ID Verification API** - OCR-based identity verification
6. **Depression Prediction API** - Mental health assessment

---

## Quick Start - All APIs

```bash
# Start all APIs at once (Recommended)
start_all_apis.bat
```

This will start:
- Content Moderation API (Legacy): http://localhost:5050
- **Intelligent Moderation API (NEW)**: http://localhost:5051
- Elder Wellness API: http://localhost:5002
- Multimodal Item Matching API: http://localhost:5003
- ID Verification API: http://localhost:5004
- Depression Prediction API: http://localhost:5007

---

## 1. Content Moderation Systems

### A) Legacy System (Port 5050)
ML-based content moderation using Logistic Regression + TF-IDF for detecting toxic comments and posts.

**Features:**
- Text-only analysis
- Fast (~50ms per request)
- Lightweight (~10MB)
- 95% accuracy

### B) Intelligent Moderation System (Port 5051) ⭐ NEW
Research-level intelligent moderation combining:
- **Text Analysis**: BERT/DistilBERT or TF-IDF
- **Image Analysis**: CLIP (Vision Transformer)
- **User Behavior**: Graph Neural Networks (GNN)

**Features:**
- Multimodal (text + image)
- User trust scoring
- Context-aware decisions
- 4-level recommendations (block/review/flag/approve)
- 97-98% accuracy
- Explainable AI

**Documentation:**
- `INTELLIGENT_MODERATION_README.md` - Complete guide
- `INTEGRATION_GUIDE.md` - C# integration
- `SYSTEM_COMPARISON.md` - Legacy vs Intelligent comparison
- `demo_intelligent_moderation.py` - Demo script

**Running:**
```bash
# Windows
start_intelligent_moderation_api.bat

# Or manually
python intelligent_moderation_api.py
```

**Quick Demo:**
```bash
python demo_intelligent_moderation.py
```

### Model Files

Pre-trained models are stored in `models/` directory:
- `toxic_classifier.pkl` - Logistic Regression classifier
- `tfidf_vectorizer.pkl` - TF-IDF vectorizer

### Running the Legacy Content Moderation API

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

## 3. Item Matching API (S-BERT)

Semantic similarity matching using Sentence-BERT (S-BERT) for the Lost & Found module. When a user reports a found item, the system automatically checks for similar lost item reports and notifies potential owners.

### Model

Uses the `all-MiniLM-L6-v2` S-BERT model from Hugging Face - a lightweight but effective model for semantic similarity.

### Running the Item Matching API

**Option 1: Using batch file (Recommended)**
```bash
start_item_matching_api.bat
```

**Option 2: Manual steps**
```bash
cd backend/ML

# Create virtual environment (first time)
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
pip install sentence-transformers torch

# Start the API
python item_matching_api.py
```

The API runs on `http://localhost:5003`

### API Endpoints

#### Health Check
```
GET /health
```

#### Find Matching Items
```
POST /match
Content-Type: application/json

{
    "query_item": {
        "id": "found_item_123",
        "title": "Black Leather Wallet",
        "category": "Wallet",
        "description": "Contains credit cards and ID",
        "location": "Central Park"
    },
    "candidate_items": [
        {
            "id": "lost_item_1",
            "user_id": "user_123",
            "title": "Lost Wallet",
            "category": "Wallet",
            "description": "Black wallet with cards",
            "location": "Park area"
        }
    ],
    "threshold": 0.5,
    "top_k": 5
}
```

Response:
```json
{
    "success": true,
    "matches": [
        {
            "item": {...},
            "similarity": 0.85,
            "match_percentage": 85.0
        }
    ],
    "query_item_id": "found_item_123",
    "total_candidates": 1,
    "matches_found": 1
}
```

#### Compute Similarity Between Two Items
```
POST /similarity
Content-Type: application/json

{
    "item1": {"title": "...", "category": "...", "description": "..."},
    "item2": {"title": "...", "category": "...", "description": "..."}
}
```

### How It Works

1. When a user reports a **found item**, the system:
   - Fetches all active lost item reports
   - Uses S-BERT to compute semantic similarity between the found item and each lost report
   - If similarity >= 50% (configurable), notifies the lost item owner

2. When a user reports a **lost item**, the system:
   - Fetches all available found items
   - Uses S-BERT to find potential matches
   - Notifies the user about any matching found items

3. **Category Boost**: If categories match exactly, similarity score gets a 15% boost

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
    },
    "ItemMatching": {
        "ApiUrl": "http://localhost:5003",
        "Threshold": "0.5"
    }
}
```

## Docker Deployment

All APIs are included in docker-compose.yml:
- `ml-api` - Content Moderation (port 5050)
- `wellness-api` - Elder Wellness (port 5002)
- `item-matching-api` - Item Matching S-BERT (port 5003)

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

3. **Start Item Matching API** (for Lost & Found module):
   ```bash
   cd backend/ML
   start_item_matching_api.bat
   ```

4. **Start the main .NET application**:
   ```bash
   cd backend
   dotnet run
   ```
