"""
Depression Prediction Model Training Script
Uses XGBoost Classifier to predict depression status
Based on mental health and lifestyle factors for students and working professionals
"""

import os
import pickle
import pandas as pd
import numpy as np
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder
from sklearn.metrics import classification_report, accuracy_score, f1_score, confusion_matrix
from xgboost import XGBClassifier
import warnings
warnings.filterwarnings('ignore')

# Paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_PATH = os.path.join(SCRIPT_DIR, 'Data', 'final_depression_dataset_1.csv')
MODEL_PATH = os.path.join(SCRIPT_DIR, 'models')

# Features for different user types
COMMON_FEATURES = [
    'Gender', 'Age', 'Sleep Duration', 'Dietary Habits', 
    'Work/Study Hours', 'Financial Stress', 'Family History of Mental Illness',
    'Have you ever had suicidal thoughts ?'
]

STUDENT_FEATURES = ['Academic Pressure', 'CGPA', 'Study Satisfaction']
WORKING_FEATURES = ['Work Pressure', 'Job Satisfaction']

# Categorical columns that need encoding
CATEGORICAL_COLUMNS = ['Gender', 'Sleep Duration', 'Dietary Habits', 
                        'Family History of Mental Illness', 'Have you ever had suicidal thoughts ?']

def preprocess_data(df):
    """Preprocess the dataset - fill null values and prepare features"""
    
    # Fill null values based on user type
    # For students: fill work-related nulls
    student_mask = df['Working Professional or Student'] == 'Student'
    working_mask = df['Working Professional or Student'] == 'Working Professional'
    
    # Fill student-specific nulls with median values
    df.loc[student_mask, 'Academic Pressure'] = df.loc[student_mask, 'Academic Pressure'].fillna(
        df.loc[student_mask, 'Academic Pressure'].median() if df.loc[student_mask, 'Academic Pressure'].notna().any() else 3
    )
    df.loc[student_mask, 'CGPA'] = df.loc[student_mask, 'CGPA'].fillna(
        df.loc[student_mask, 'CGPA'].median() if df.loc[student_mask, 'CGPA'].notna().any() else 7.0
    )
    df.loc[student_mask, 'Study Satisfaction'] = df.loc[student_mask, 'Study Satisfaction'].fillna(
        df.loc[student_mask, 'Study Satisfaction'].median() if df.loc[student_mask, 'Study Satisfaction'].notna().any() else 3
    )
    
    # Fill working professional-specific nulls with median values
    df.loc[working_mask, 'Work Pressure'] = df.loc[working_mask, 'Work Pressure'].fillna(
        df.loc[working_mask, 'Work Pressure'].median() if df.loc[working_mask, 'Work Pressure'].notna().any() else 3
    )
    df.loc[working_mask, 'Job Satisfaction'] = df.loc[working_mask, 'Job Satisfaction'].fillna(
        df.loc[working_mask, 'Job Satisfaction'].median() if df.loc[working_mask, 'Job Satisfaction'].notna().any() else 3
    )
    
    # For students, set work features to neutral (3)
    df.loc[student_mask, 'Work Pressure'] = df.loc[student_mask, 'Work Pressure'].fillna(3)
    df.loc[student_mask, 'Job Satisfaction'] = df.loc[student_mask, 'Job Satisfaction'].fillna(3)
    
    # For working professionals, set student features to neutral
    df.loc[working_mask, 'Academic Pressure'] = df.loc[working_mask, 'Academic Pressure'].fillna(3)
    df.loc[working_mask, 'CGPA'] = df.loc[working_mask, 'CGPA'].fillna(7.0)
    df.loc[working_mask, 'Study Satisfaction'] = df.loc[working_mask, 'Study Satisfaction'].fillna(3)
    
    # Fill any remaining nulls
    df['Academic Pressure'] = df['Academic Pressure'].fillna(3)
    df['Work Pressure'] = df['Work Pressure'].fillna(3)
    df['CGPA'] = df['CGPA'].fillna(7.0)
    df['Study Satisfaction'] = df['Study Satisfaction'].fillna(3)
    df['Job Satisfaction'] = df['Job Satisfaction'].fillna(3)
    
    return df

def train_models():
    """Train XGBoost models for depression prediction"""
    
    print("=" * 60)
    print("DEPRESSION PREDICTION MODEL TRAINING (XGBoost)")
    print("=" * 60)
    
    # Check if dataset exists
    if not os.path.exists(DATA_PATH):
        print(f"\n❌ ERROR: Dataset not found at {DATA_PATH}")
        return False
    
    # Load dataset
    print(f"\n📂 Loading dataset from: {DATA_PATH}")
    df = pd.read_csv(DATA_PATH)
    print(f"✅ Dataset loaded successfully!")
    print(f"   - Total records: {len(df)}")
    print(f"   - Features: {len(df.columns)}")
    
    # Preprocess data
    print(f"\n🔄 Preprocessing data...")
    df = preprocess_data(df)
    
    # Initialize encoders
    encoders = {}
    models = {}
    
    # Encode categorical variables
    print(f"\n🔄 Encoding categorical variables...")
    for col in CATEGORICAL_COLUMNS:
        encoders[col] = LabelEncoder()
        df[col + '_encoded'] = encoders[col].fit_transform(df[col].astype(str))
        print(f"   - {col}: {list(encoders[col].classes_)}")
    
    # Encode user type
    encoders['Working Professional or Student'] = LabelEncoder()
    df['UserType_encoded'] = encoders['Working Professional or Student'].fit_transform(
        df['Working Professional or Student'].astype(str)
    )
    print(f"   - Working Professional or Student: {list(encoders['Working Professional or Student'].classes_)}")
    
    # Prepare feature columns
    ALL_FEATURES = COMMON_FEATURES + STUDENT_FEATURES + WORKING_FEATURES
    
    feature_cols = []
    for col in ALL_FEATURES:
        if col in CATEGORICAL_COLUMNS:
            feature_cols.append(col + '_encoded')
        else:
            feature_cols.append(col)
    
    # Add user type as feature
    feature_cols.append('UserType_encoded')
    
    X = df[feature_cols].astype(float)
    
    # Prepare target
    encoders['Depression'] = LabelEncoder()
    y = encoders['Depression'].fit_transform(df['Depression'])
    
    print(f"\n📐 Feature matrix shape: {X.shape}")
    print(f"📊 Target distribution: {dict(zip(encoders['Depression'].classes_, np.bincount(y)))}")
    
    # Split data
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )
    
    print(f"\nTraining set: {len(X_train)} samples")
    print(f"Test set: {len(X_test)} samples")
    
    # Train XGBoost model
    print("\n" + "=" * 60)
    print("TRAINING XGBoost CLASSIFIER")
    print("=" * 60)
    
    # Calculate scale_pos_weight for imbalanced classes
    scale_pos_weight = len(y_train[y_train == 0]) / max(len(y_train[y_train == 1]), 1)
    
    models['depression'] = XGBClassifier(
        n_estimators=200,
        max_depth=6,
        learning_rate=0.1,
        subsample=0.8,
        colsample_bytree=0.8,
        scale_pos_weight=scale_pos_weight,
        random_state=42,
        use_label_encoder=False,
        eval_metric='logloss'
    )
    
    print("\n🏋️ Training XGBoost Classifier...")
    models['depression'].fit(X_train, y_train)
    
    # Evaluate
    y_pred = models['depression'].predict(X_test)
    y_proba = models['depression'].predict_proba(X_test)
    
    accuracy = accuracy_score(y_test, y_pred)
    f1 = f1_score(y_test, y_pred, average='weighted')
    
    print(f"\n✅ Model trained!")
    print(f"   - Accuracy: {accuracy:.2%}")
    print(f"   - F1 Score: {f1:.2%}")
    
    print("\n📊 Classification Report:")
    print(classification_report(y_test, y_pred, target_names=encoders['Depression'].classes_))
    
    print("\n📊 Confusion Matrix:")
    cm = confusion_matrix(y_test, y_pred)
    print(f"   True Negatives: {cm[0][0]}, False Positives: {cm[0][1]}")
    print(f"   False Negatives: {cm[1][0]}, True Positives: {cm[1][1]}")
    
    # Feature importance
    print("\n📊 Top 10 Feature Importances:")
    importance = models['depression'].feature_importances_
    feature_importance = sorted(zip(feature_cols, importance), key=lambda x: x[1], reverse=True)
    for feat, imp in feature_importance[:10]:
        print(f"   - {feat}: {imp:.4f}")
    
    # Save models and encoders
    os.makedirs(MODEL_PATH, exist_ok=True)
    model_file = os.path.join(MODEL_PATH, 'depression_model.pkl')
    
    with open(model_file, 'wb') as f:
        pickle.dump({
            'models': models,
            'encoders': encoders,
            'feature_columns': feature_cols,
            'categorical_columns': CATEGORICAL_COLUMNS,
            'common_features': COMMON_FEATURES,
            'student_features': STUDENT_FEATURES,
            'working_features': WORKING_FEATURES
        }, f)
    
    print(f"\n✅ Model saved to: {model_file}")
    print("\n" + "=" * 60)
    print("TRAINING COMPLETE!")
    print("=" * 60)
    
    return True

if __name__ == "__main__":
    train_models()
