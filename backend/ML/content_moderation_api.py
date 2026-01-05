"""
Content Moderation API
Flask API for harmful content detection using trained ML model
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import pickle
import re
import os

app = Flask(__name__)
CORS(app)

# Model paths
MODEL_PATH = 'models/toxic_classifier.pkl'
VECTORIZER_PATH = 'models/tfidf_vectorizer.pkl'

# Global model and vectorizer
model = None
vectorizer = None

def clean_text(text):
    """Clean and preprocess text"""
    if not text:
        return ""
    text = str(text).lower()
    text = re.sub(r'http\S+|www\S+|https\S+', '', text)
    text = re.sub(r'<.*?>', '', text)
    text = re.sub(r'[^a-zA-Z\s]', ' ', text)
    text = re.sub(r'\s+', ' ', text).strip()
    return text

def load_models():
    """Load the trained model and vectorizer"""
    global model, vectorizer
    
    if not os.path.exists(MODEL_PATH) or not os.path.exists(VECTORIZER_PATH):
        print(f"ERROR: Models not found at {MODEL_PATH} and {VECTORIZER_PATH}")
        print("Please ensure pre-trained models are included in the deployment.")
        print("To train locally: python train_model.py")
        return False
    
    print("Loading pre-trained models...")
    with open(MODEL_PATH, 'rb') as f:
        model = pickle.load(f)
    
    with open(VECTORIZER_PATH, 'rb') as f:
        vectorizer = pickle.load(f)
    
    print("Models loaded successfully!")
    return True

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'model_loaded': model is not None
    })

@app.route('/predict', methods=['POST'])
def predict():
    """
    Predict if content is harmful
    
    Request body:
    {
        "text": "content to analyze",
        "threshold": 0.5  # optional, default 0.5
    }
    
    Response:
    {
        "is_harmful": true/false,
        "confidence": 0.85,
        "threshold": 0.5
    }
    """
    if model is None or vectorizer is None:
        return jsonify({'error': 'Model not loaded'}), 500
    
    data = request.get_json()
    
    if not data or 'text' not in data:
        return jsonify({'error': 'Missing text field'}), 400
    
    text = data['text']
    threshold = data.get('threshold', 0.5)
    
    if not text or len(text.strip()) == 0:
        return jsonify({
            'is_harmful': False,
            'confidence': 0.0,
            'threshold': threshold
        })
    
    # Clean and predict
    clean = clean_text(text)
    features = vectorizer.transform([clean])
    
    probability = model.predict_proba(features)[0][1]
    is_harmful = probability >= threshold
    
    return jsonify({
        'is_harmful': bool(is_harmful),
        'confidence': float(probability),
        'threshold': threshold
    })

@app.route('/predict/batch', methods=['POST'])
def predict_batch():
    """
    Predict if multiple contents are harmful
    
    Request body:
    {
        "texts": ["text1", "text2", ...],
        "threshold": 0.5  # optional
    }
    
    Response:
    {
        "results": [
            {"text": "text1", "is_harmful": false, "confidence": 0.1},
            ...
        ]
    }
    """
    if model is None or vectorizer is None:
        return jsonify({'error': 'Model not loaded'}), 500
    
    data = request.get_json()
    
    if not data or 'texts' not in data:
        return jsonify({'error': 'Missing texts field'}), 400
    
    texts = data['texts']
    threshold = data.get('threshold', 0.5)
    
    results = []
    for text in texts:
        if not text or len(str(text).strip()) == 0:
            results.append({
                'text': text,
                'is_harmful': False,
                'confidence': 0.0
            })
            continue
        
        clean = clean_text(text)
        features = vectorizer.transform([clean])
        probability = model.predict_proba(features)[0][1]
        
        results.append({
            'text': text[:100] + '...' if len(text) > 100 else text,
            'is_harmful': bool(probability >= threshold),
            'confidence': float(probability)
        })
    
    return jsonify({
        'results': results,
        'threshold': threshold
    })

if __name__ == '__main__':
    if load_models():
        print("Starting Content Moderation API on port 5050...")
        app.run(host='0.0.0.0', port=5050, debug=False)
    else:
        print("Failed to load models. Please train the model first:")
        print("  python train_model.py")
