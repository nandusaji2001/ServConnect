"""
Elder Wellness Model Training Script
Trains Random Forest Classifier models for:
1. Diet Recommendation
2. Diet Plan
3. Heart Risk

Uses the elder_diet_ml_ready_dataset_v3.csv dataset
"""

import os
import pickle
import pandas as pd
import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder
from sklearn.metrics import classification_report, accuracy_score
import warnings
warnings.filterwarnings('ignore')

# Paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_PATH = os.path.join(SCRIPT_DIR, 'Data', 'elder_diet_ml_ready_dataset_v3.csv')
MODEL_PATH = os.path.join(SCRIPT_DIR, 'models')

# Feature columns used for prediction
FEATURE_COLUMNS = [
    'age', 'gender', 'bmi', 'systolic_bp', 'diastolic_bp', 
    'cholesterol', 'triglycerides', 'family_history_t2d', 'family_history_cvd',
    'sleep_hours', 'sleep_quality', 'stress_level', 'physical_activity_level'
]

# Categorical columns that need encoding
CATEGORICAL_COLUMNS = ['gender', 'sleep_quality', 'physical_activity_level']

def train_models():
    """Train all wellness prediction models"""
    
    print("=" * 60)
    print("ELDER WELLNESS MODEL TRAINING")
    print("=" * 60)
    
    # Check if dataset exists
    if not os.path.exists(DATA_PATH):
        print(f"\n❌ ERROR: Dataset not found at {DATA_PATH}")
        print("Please ensure the dataset file exists in the Data folder.")
        return False
    
    # Load dataset
    print(f"\n📂 Loading dataset from: {DATA_PATH}")
    df = pd.read_csv(DATA_PATH)
    print(f"✅ Dataset loaded successfully!")
    print(f"   - Total records: {len(df)}")
    print(f"   - Features: {len(df.columns)}")
    
    # Display dataset info
    print(f"\n📊 Dataset columns:")
    for i, col in enumerate(df.columns[:20], 1):
        print(f"   {i}. {col}")
    if len(df.columns) > 20:
        print(f"   ... and {len(df.columns) - 20} more columns")
    
    # Initialize encoders dictionary
    encoders = {}
    models = {}
    
    # Encode categorical variables
    print(f"\n🔄 Encoding categorical variables...")
    for col in CATEGORICAL_COLUMNS:
        encoders[col] = LabelEncoder()
        df[col + '_encoded'] = encoders[col].fit_transform(df[col].astype(str))
        print(f"   - {col}: {len(encoders[col].classes_)} unique values")
    
    # Prepare feature matrix
    feature_cols = []
    for col in FEATURE_COLUMNS:
        if col in CATEGORICAL_COLUMNS:
            feature_cols.append(col + '_encoded')
        else:
            feature_cols.append(col)
    
    X = df[feature_cols].fillna(0)
    print(f"\n📐 Feature matrix shape: {X.shape}")
    
    # ========================================
    # Train Diet Recommendation Model
    # ========================================
    print("\n" + "=" * 60)
    print("TRAINING MODEL 1: Diet Recommendation")
    print("=" * 60)
    
    y_diet_rec = df['diet_recommendation'].fillna('Balanced whole-food diet.')
    encoders['diet_recommendation'] = LabelEncoder()
    y_diet_rec_encoded = encoders['diet_recommendation'].fit_transform(y_diet_rec)
    
    print(f"Target classes: {len(encoders['diet_recommendation'].classes_)}")
    for i, cls in enumerate(encoders['diet_recommendation'].classes_[:5]):
        print(f"   {i}: {cls[:50]}...")
    
    X_train, X_test, y_train, y_test = train_test_split(
        X, y_diet_rec_encoded, test_size=0.2, random_state=42
    )
    
    print(f"\nTraining set: {len(X_train)} samples")
    print(f"Test set: {len(X_test)} samples")
    
    models['diet_recommendation'] = RandomForestClassifier(
        n_estimators=100, 
        random_state=42, 
        n_jobs=-1,
        max_depth=10
    )
    
    print("\n🏋️ Training Random Forest Classifier...")
    models['diet_recommendation'].fit(X_train, y_train)
    
    # Evaluate
    y_pred = models['diet_recommendation'].predict(X_test)
    accuracy = accuracy_score(y_test, y_pred)
    print(f"✅ Model trained! Accuracy: {accuracy:.2%}")
    
    # ========================================
    # Train Diet Plan Model
    # ========================================
    print("\n" + "=" * 60)
    print("TRAINING MODEL 2: Diet Plan")
    print("=" * 60)
    
    y_diet_plan = df['diet_plan'].fillna('vegetarian')
    encoders['diet_plan'] = LabelEncoder()
    y_diet_plan_encoded = encoders['diet_plan'].fit_transform(y_diet_plan)
    
    print(f"Target classes: {len(encoders['diet_plan'].classes_)}")
    for cls in encoders['diet_plan'].classes_:
        print(f"   - {cls}")
    
    X_train, X_test, y_train, y_test = train_test_split(
        X, y_diet_plan_encoded, test_size=0.2, random_state=42
    )
    
    models['diet_plan'] = RandomForestClassifier(
        n_estimators=100, 
        random_state=42, 
        n_jobs=-1,
        max_depth=10
    )
    
    print("\n🏋️ Training Random Forest Classifier...")
    models['diet_plan'].fit(X_train, y_train)
    
    y_pred = models['diet_plan'].predict(X_test)
    accuracy = accuracy_score(y_test, y_pred)
    print(f"✅ Model trained! Accuracy: {accuracy:.2%}")
    
    # ========================================
    # Train Heart Risk Model
    # ========================================
    print("\n" + "=" * 60)
    print("TRAINING MODEL 3: Heart Risk")
    print("=" * 60)
    
    y_heart_risk = df['heart_risk'].fillna('moderate')
    encoders['heart_risk'] = LabelEncoder()
    y_heart_risk_encoded = encoders['heart_risk'].fit_transform(y_heart_risk)
    
    print(f"Target classes: {len(encoders['heart_risk'].classes_)}")
    for cls in encoders['heart_risk'].classes_:
        count = (y_heart_risk == cls).sum()
        print(f"   - {cls}: {count} samples ({count/len(y_heart_risk)*100:.1f}%)")
    
    X_train, X_test, y_train, y_test = train_test_split(
        X, y_heart_risk_encoded, test_size=0.2, random_state=42
    )
    
    models['heart_risk'] = RandomForestClassifier(
        n_estimators=100, 
        random_state=42, 
        n_jobs=-1,
        max_depth=10
    )
    
    print("\n🏋️ Training Random Forest Classifier...")
    models['heart_risk'].fit(X_train, y_train)
    
    y_pred = models['heart_risk'].predict(X_test)
    accuracy = accuracy_score(y_test, y_pred)
    print(f"✅ Model trained! Accuracy: {accuracy:.2%}")
    
    # Print detailed classification report
    print("\n📋 Classification Report:")
    print(classification_report(y_test, y_pred, 
          target_names=encoders['heart_risk'].classes_))
    
    # ========================================
    # Save Models
    # ========================================
    print("\n" + "=" * 60)
    print("SAVING MODELS")
    print("=" * 60)
    
    os.makedirs(MODEL_PATH, exist_ok=True)
    model_file = os.path.join(MODEL_PATH, 'wellness_models.pkl')
    
    with open(model_file, 'wb') as f:
        pickle.dump({
            'models': models,
            'encoders': encoders
        }, f)
    
    print(f"✅ Models saved to: {model_file}")
    
    # Verify saved models
    file_size = os.path.getsize(model_file) / 1024
    print(f"   File size: {file_size:.2f} KB")
    
    print("\n" + "=" * 60)
    print("TRAINING COMPLETE!")
    print("=" * 60)
    print("\nYou can now run the wellness API with:")
    print("   python elder_wellness_api.py")
    print("\nOr use the batch file:")
    print("   start_wellness_api.bat")
    
    return True

if __name__ == '__main__':
    train_models()
