"""
OCR Text Extraction API
Extracts text from images using EasyOCR for content moderation
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import easyocr
import numpy as np
from PIL import Image
import io
import base64
import os

app = Flask(__name__)
CORS(app)

# Global OCR reader
reader = None

def load_ocr_reader():
    """Initialize EasyOCR reader"""
    global reader
    print("Loading EasyOCR reader (English)...")
    reader = easyocr.Reader(['en'], gpu=False)
    print("OCR reader loaded successfully!")
    return True

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'ocr_loaded': reader is not None
    })

@app.route('/extract-text', methods=['POST'])
def extract_text():
    """
    Extract text from image
    
    Request body:
    {
        "image": "base64_encoded_image"
    }
    
    Response:
    {
        "text": "extracted text",
        "confidence": 0.85,
        "details": [
            {"text": "line1", "confidence": 0.9},
            {"text": "line2", "confidence": 0.8}
        ]
    }
    """
    if reader is None:
        return jsonify({'error': 'OCR reader not loaded'}), 500
    
    data = request.get_json()
    
    if not data or 'image' not in data:
        return jsonify({'error': 'Missing image field'}), 400
    
    try:
        # Decode base64 image
        image_data = data['image']
        if image_data.startswith('data:image'):
            image_data = image_data.split(',')[1]
        
        image_bytes = base64.b64decode(image_data)
        image = Image.open(io.BytesIO(image_bytes))
        
        # Convert to RGB if needed
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        # Convert to numpy array
        image_np = np.array(image)
        
        # Extract text using EasyOCR
        results = reader.readtext(image_np)
        
        # Process results
        extracted_texts = []
        confidences = []
        
        for (bbox, text, confidence) in results:
            extracted_texts.append(text)
            confidences.append(confidence)
        
        # Combine all text
        full_text = ' '.join(extracted_texts)
        avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
        
        # Prepare detailed results
        details = [
            {'text': text, 'confidence': float(conf)}
            for (_, text, conf) in results
        ]
        
        return jsonify({
            'text': full_text,
            'confidence': float(avg_confidence),
            'details': details,
            'has_text': len(extracted_texts) > 0
        })
    
    except Exception as e:
        return jsonify({'error': str(e)}), 400

@app.route('/extract-text/batch', methods=['POST'])
def extract_text_batch():
    """
    Extract text from multiple images
    
    Request body:
    {
        "images": ["base64_1", "base64_2", ...]
    }
    
    Response:
    {
        "results": [
            {"text": "...", "confidence": 0.85},
            ...
        ]
    }
    """
    if reader is None:
        return jsonify({'error': 'OCR reader not loaded'}), 500
    
    data = request.get_json()
    
    if not data or 'images' not in data:
        return jsonify({'error': 'Missing images field'}), 400
    
    results = []
    
    for image_data in data['images']:
        try:
            # Decode base64 image
            if image_data.startswith('data:image'):
                image_data = image_data.split(',')[1]
            
            image_bytes = base64.b64decode(image_data)
            image = Image.open(io.BytesIO(image_bytes))
            
            if image.mode != 'RGB':
                image = image.convert('RGB')
            
            image_np = np.array(image)
            
            # Extract text
            ocr_results = reader.readtext(image_np)
            
            extracted_texts = [text for (_, text, _) in ocr_results]
            confidences = [conf for (_, _, conf) in ocr_results]
            
            full_text = ' '.join(extracted_texts)
            avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
            
            results.append({
                'text': full_text,
                'confidence': float(avg_confidence),
                'has_text': len(extracted_texts) > 0
            })
        
        except Exception as e:
            results.append({
                'text': '',
                'confidence': 0.0,
                'has_text': False,
                'error': str(e)
            })
    
    return jsonify({'results': results})

if __name__ == '__main__':
    if load_ocr_reader():
        print("Starting OCR Text Extraction API on port 5008...")
        app.run(host='0.0.0.0', port=5008, debug=False)
    else:
        print("Failed to load OCR reader")
