"""
Text Model - Transformer-based Toxic Content Detection
Supports both legacy TF-IDF + Logistic Regression and modern BERT-based models
"""

import torch
import pickle
import re
import os
from typing import Dict, Optional
from transformers import AutoTokenizer, AutoModelForSequenceClassification
import numpy as np

class TextModerationModel:
    """Unified text moderation supporting both legacy and transformer models"""
    
    def __init__(self, model_type: str = "legacy", model_name: str = "distilbert-base-uncased-finetuned-sst-2-english"):
        """
        Initialize text moderation model
        
        Args:
            model_type: "legacy" (TF-IDF + LR) or "transformer" (BERT/DistilBERT)
            model_name: HuggingFace model name for transformer mode
        """
        self.model_type = model_type
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        
        if model_type == "legacy":
            self._load_legacy_model()
        elif model_type == "transformer":
            self._load_transformer_model(model_name)
        else:
            raise ValueError(f"Unknown model_type: {model_type}")
        
        print(f"Text moderation model loaded: {model_type} on {self.device}")
    
    def _load_legacy_model(self):
        """Load TF-IDF + Logistic Regression model"""
        model_path = 'models/toxic_classifier.pkl'
        vectorizer_path = 'models/tfidf_vectorizer.pkl'
        
        if not os.path.exists(model_path) or not os.path.exists(vectorizer_path):
            raise FileNotFoundError(f"Legacy models not found at {model_path}")
        
        with open(model_path, 'rb') as f:
            self.model = pickle.load(f)
        
        with open(vectorizer_path, 'rb') as f:
            self.vectorizer = pickle.load(f)
        
        self.tokenizer = None
    
    def _load_transformer_model(self, model_name: str):
        """Load transformer-based model (BERT/DistilBERT)"""
        print(f"Loading transformer model: {model_name}")
        
        # For demo, we use a sentiment model as proxy for toxicity
        # In production, use: unitary/toxic-bert or martin-ha/toxic-comment-model
        self.tokenizer = AutoTokenizer.from_pretrained(model_name)
        self.model = AutoModelForSequenceClassification.from_pretrained(model_name)
        self.model = self.model.to(self.device)
        self.model.eval()
        self.vectorizer = None
    
    def clean_text(self, text: str) -> str:
        """Clean and preprocess text"""
        if not text:
            return ""
        text = str(text).lower()
        text = re.sub(r'http\S+|www\S+|https\S+', '', text)
        text = re.sub(r'<.*?>', '', text)
        text = re.sub(r'[^a-zA-Z\s]', ' ', text)
        text = re.sub(r'\s+', ' ', text).strip()
        return text
    
    def predict(self, text: str) -> Dict[str, float]:
        """
        Predict toxicity score for text
        
        Returns:
            Dict with 'toxicity_score' (0-1, higher = more toxic)
        """
        if not text or len(text.strip()) == 0:
            return {'toxicity_score': 0.0}
        
        if self.model_type == "legacy":
            return self._predict_legacy(text)
        else:
            return self._predict_transformer(text)
    
    def _predict_legacy(self, text: str) -> Dict[str, float]:
        """Predict using TF-IDF + Logistic Regression"""
        clean = self.clean_text(text)
        features = self.vectorizer.transform([clean])
        probability = self.model.predict_proba(features)[0][1]
        
        return {
            'toxicity_score': float(probability)
        }
    
    def _predict_transformer(self, text: str) -> Dict[str, float]:
        """Predict using transformer model"""
        # Tokenize
        inputs = self.tokenizer(
            text,
            return_tensors="pt",
            truncation=True,
            max_length=512,
            padding=True
        ).to(self.device)
        
        # Predict
        with torch.no_grad():
            outputs = self.model(**inputs)
            logits = outputs.logits
            probs = torch.softmax(logits, dim=-1)
            
            # For sentiment models: negative class = toxic
            # Adjust based on your model's label mapping
            toxicity_score = float(probs[0][0].item())  # Negative class probability
        
        return {
            'toxicity_score': toxicity_score
        }
    
    def predict_batch(self, texts: list) -> list:
        """Predict toxicity for multiple texts"""
        return [self.predict(text) for text in texts]


# For backward compatibility
def load_legacy_model():
    """Load legacy model (for existing code)"""
    return TextModerationModel(model_type="legacy")
