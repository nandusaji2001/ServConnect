"""
Harmful Content Detection Model Training
Uses Logistic Regression + TF-IDF to detect toxic comments
"""

import pandas as pd
import numpy as np
import pickle
import os
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, accuracy_score
import re
import warnings
warnings.filterwarnings('ignore')

# Configuration
DATA_PATH = 'Data/train.csv'
MODEL_PATH = 'models/toxic_classifier.pkl'
VECTORIZER_PATH = 'models/tfidf_vectorizer.pkl'

def clean_text(text):
    """Clean and preprocess text"""
    if pd.isna(text):
        return ""
    text = str(text).lower()
    # Remove URLs
    text = re.sub(r'http\S+|www\S+|https\S+', '', text)
    # Remove HTML tags
    text = re.sub(r'<.*?>', '', text)
    # Remove special characters but keep spaces
    text = re.sub(r'[^a-zA-Z\s]', ' ', text)
    # Remove extra whitespace
    text = re.sub(r'\s+', ' ', text).strip()
    return text

def load_and_prepare_data():
    """Load and prepare the training data"""
    print("Loading data...")
    df = pd.read_csv(DATA_PATH)
    
    print(f"Dataset shape: {df.shape}")
    print(f"Columns: {df.columns.tolist()}")
    
    # Clean the text
    print("Cleaning text...")
    df['clean_text'] = df['comment_text'].apply(clean_text)
    
    # Create binary toxic label (1 if any toxic category is 1)
    toxic_columns = ['toxic', 'severe_toxic', 'obscene', 'threat', 'insult', 'identity_hate']
    df['is_harmful'] = df[toxic_columns].max(axis=1)
    
    print(f"\nClass distribution:")
    print(df['is_harmful'].value_counts())
    print(f"\nHarmful content ratio: {df['is_harmful'].mean():.2%}")
    
    return df

def train_model(df):
    """Train the Logistic Regression model with TF-IDF features"""
    print("\nPreparing features...")
    
    X = df['clean_text']
    y = df['is_harmful']
    
    # Split data
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42, stratify=y
    )
    
    print(f"Training samples: {len(X_train)}")
    print(f"Test samples: {len(X_test)}")
    
    # TF-IDF Vectorization
    print("\nCreating TF-IDF features...")
    vectorizer = TfidfVectorizer(
        max_features=10000,
        ngram_range=(1, 2),
        min_df=2,
        max_df=0.95,
        stop_words='english'
    )
    
    X_train_tfidf = vectorizer.fit_transform(X_train)
    X_test_tfidf = vectorizer.transform(X_test)
    
    print(f"TF-IDF feature shape: {X_train_tfidf.shape}")
    
    # Train Logistic Regression
    print("\nTraining Logistic Regression model...")
    model = LogisticRegression(
        C=1.0,
        max_iter=1000,
        class_weight='balanced',
        solver='lbfgs',
        n_jobs=-1,
        random_state=42
    )
    
    model.fit(X_train_tfidf, y_train)
    
    # Evaluate
    print("\nEvaluating model...")
    y_pred = model.predict(X_test_tfidf)
    y_pred_proba = model.predict_proba(X_test_tfidf)[:, 1]
    
    print(f"\nAccuracy: {accuracy_score(y_test, y_pred):.4f}")
    print("\nClassification Report:")
    print(classification_report(y_test, y_pred, target_names=['Safe', 'Harmful']))
    
    return model, vectorizer

def save_model(model, vectorizer):
    """Save the trained model and vectorizer"""
    os.makedirs('models', exist_ok=True)
    
    print(f"\nSaving model to {MODEL_PATH}...")
    with open(MODEL_PATH, 'wb') as f:
        pickle.dump(model, f)
    
    print(f"Saving vectorizer to {VECTORIZER_PATH}...")
    with open(VECTORIZER_PATH, 'wb') as f:
        pickle.dump(vectorizer, f)
    
    print("Models saved successfully!")

def test_predictions(model, vectorizer):
    """Test the model with sample texts"""
    test_texts = [
        "Hello, how are you doing today?",
        "This is a great post, thanks for sharing!",
        "You are an idiot and I hate you",
        "I will kill you",
        "What a beautiful day!",
        "You're so stupid, go die",
        "Thanks for the helpful information",
        "I disagree with your opinion but respect it"
    ]
    
    print("\n" + "="*60)
    print("Sample Predictions:")
    print("="*60)
    
    for text in test_texts:
        clean = clean_text(text)
        features = vectorizer.transform([clean])
        pred = model.predict(features)[0]
        prob = model.predict_proba(features)[0][1]
        
        status = "🚫 HARMFUL" if pred == 1 else "✅ SAFE"
        print(f"\nText: {text[:50]}...")
        print(f"Prediction: {status} (confidence: {prob:.2%})")

def main():
    print("="*60)
    print("Harmful Content Detection - Model Training")
    print("="*60)
    
    # Load and prepare data
    df = load_and_prepare_data()
    
    # Train model
    model, vectorizer = train_model(df)
    
    # Save model
    save_model(model, vectorizer)
    
    # Test predictions
    test_predictions(model, vectorizer)
    
    print("\n" + "="*60)
    print("Training complete!")
    print("="*60)

if __name__ == "__main__":
    main()
