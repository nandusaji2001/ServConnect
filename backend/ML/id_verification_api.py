"""
ID Verification API
Flask API for OCR-based identity card verification.
Extracts name from ID card images and compares with user-provided name.
Uses EasyOCR for text extraction and fuzzy matching for name comparison.
"""

import os
import re
import base64
import tempfile
from flask import Flask, request, jsonify
from flask_cors import CORS
import easyocr
from difflib import SequenceMatcher
from PIL import Image
import io

app = Flask(__name__)
CORS(app)

# Global OCR reader
reader = None

def load_ocr_reader():
    """Load the EasyOCR reader"""
    global reader
    try:
        print("Loading EasyOCR model...")
        # Support for English and common Indian languages
        reader = easyocr.Reader(['en'], gpu=False)
        print("EasyOCR model loaded successfully!")
        return True
    except Exception as e:
        print(f"Error loading OCR model: {e}")
        return False

def clean_name(name):
    """Clean and normalize name for comparison"""
    if not name:
        return ""
    # Convert to lowercase
    name = name.lower()
    # Remove extra whitespace
    name = re.sub(r'\s+', ' ', name).strip()
    # Remove special characters but keep spaces
    name = re.sub(r'[^a-z\s]', '', name)
    return name

def extract_name_from_text(ocr_results):
    """
    Extract potential name from OCR results.
    Looks for common patterns in Indian ID cards.
    """
    all_text = []
    potential_names = []
    
    for detection in ocr_results:
        text = detection[1].strip()
        confidence = detection[2]
        
        if len(text) > 2:  # Skip very short strings
            all_text.append({
                'text': text,
                'confidence': confidence
            })
    
    # Common patterns to identify name in Indian ID cards
    name_indicators = [
        'name', 'naam', 'father', 'husband', 'mother', 'son', 'daughter',
        'holder', 'applicant', 'voter', 'elector'
    ]
    
    for i, item in enumerate(all_text):
        text_lower = item['text'].lower()
        
        # Check if this line contains a name indicator
        for indicator in name_indicators:
            if indicator in text_lower:
                # The name is likely in the same line after the indicator
                parts = re.split(r'[:\-]', item['text'])
                if len(parts) > 1:
                    potential_name = parts[-1].strip()
                    if len(potential_name) > 2 and potential_name.replace(' ', '').isalpha():
                        potential_names.append({
                            'name': potential_name,
                            'confidence': item['confidence'],
                            'source': 'same_line'
                        })
                # Or the name is on the next line
                elif i + 1 < len(all_text):
                    next_text = all_text[i + 1]['text']
                    if len(next_text) > 2 and re.match(r'^[A-Za-z\s]+$', next_text.strip()):
                        potential_names.append({
                            'name': next_text.strip(),
                            'confidence': all_text[i + 1]['confidence'],
                            'source': 'next_line'
                        })
        
        # Also check for standalone names (lines that are purely alphabetic)
        if re.match(r'^[A-Za-z\s]{3,50}$', item['text'].strip()):
            # Skip common labels
            skip_words = ['government', 'india', 'republic', 'identity', 'card', 'aadhar', 
                          'aadhaar', 'voter', 'election', 'commission', 'permanent', 
                          'account', 'number', 'income', 'tax', 'department', 'male', 
                          'female', 'address', 'date', 'birth', 'dob']
            
            if not any(skip in item['text'].lower() for skip in skip_words):
                words = item['text'].strip().split()
                # Names usually have 2-4 words and each word starts with uppercase
                if 1 <= len(words) <= 5:
                    is_name_like = all(word[0].isupper() if word else False for word in words)
                    if is_name_like or item['confidence'] > 0.7:
                        potential_names.append({
                            'name': item['text'].strip(),
                            'confidence': item['confidence'],
                            'source': 'standalone'
                        })
    
    return potential_names, all_text

def calculate_similarity(name1, name2):
    """Calculate similarity between two names (case-insensitive)"""
    clean1 = clean_name(name1)
    clean2 = clean_name(name2)
    
    if not clean1 or not clean2:
        return 0.0
    
    # Use SequenceMatcher for fuzzy matching
    return SequenceMatcher(None, clean1, clean2).ratio()

def find_best_match(user_name, potential_names, threshold=0.75):
    """
    Find the best matching name from potential names extracted from ID.
    Returns match result with similarity score.
    """
    if not potential_names:
        return None, 0.0
    
    best_match = None
    best_similarity = 0.0
    
    for potential in potential_names:
        extracted_name = potential['name']
        similarity = calculate_similarity(user_name, extracted_name)
        
        if similarity > best_similarity:
            best_similarity = similarity
            best_match = {
                'extracted_name': extracted_name,
                'similarity': similarity,
                'ocr_confidence': potential['confidence'],
                'source': potential['source']
            }
    
    return best_match, best_similarity

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'ocr_loaded': reader is not None,
        'service': 'id-verification'
    })

@app.route('/verify', methods=['POST'])
def verify_identity():
    """
    Verify if the name on ID card matches the user-provided name.
    
    Request body:
    {
        "user_name": "John Doe",
        "image_base64": "base64_encoded_image_data",
        "image_path": "/path/to/image.jpg",  # Alternative to base64
        "threshold": 0.75  # Optional, default 0.75
    }
    
    Response:
    {
        "verified": true/false,
        "auto_approved": true/false,
        "similarity_score": 0.92,
        "extracted_names": [...],
        "best_match": {...},
        "message": "Name verified successfully"
    }
    """
    if reader is None:
        return jsonify({'error': 'OCR model not loaded'}), 500
    
    data = request.get_json()
    
    if not data or 'user_name' not in data:
        return jsonify({'error': 'Missing user_name field'}), 400
    
    user_name = data['user_name']
    threshold = data.get('threshold', 0.75)
    
    # Get image data
    image = None
    temp_file = None
    
    try:
        if 'image_base64' in data:
            # Decode base64 image
            image_data = base64.b64decode(data['image_base64'])
            image = Image.open(io.BytesIO(image_data))
            
            # Convert RGBA to RGB if necessary (JPEG doesn't support transparency)
            if image.mode == 'RGBA':
                # Create a white background
                background = Image.new('RGB', image.size, (255, 255, 255))
                background.paste(image, mask=image.split()[3])  # Use alpha channel as mask
                image = background
            elif image.mode != 'RGB':
                image = image.convert('RGB')
            
            # Save to temp file for EasyOCR
            temp_file = tempfile.NamedTemporaryFile(suffix='.jpg', delete=False)
            image.save(temp_file.name, 'JPEG', quality=95)
            image_path = temp_file.name
            
        elif 'image_path' in data:
            image_path = data['image_path']
            if not os.path.exists(image_path):
                return jsonify({'error': f'Image file not found: {image_path}'}), 400
        else:
            return jsonify({'error': 'Missing image_base64 or image_path'}), 400
        
        # Perform OCR
        print(f"Processing image for user: {user_name}")
        ocr_results = reader.readtext(image_path)
        
        # Extract potential names
        potential_names, all_text = extract_name_from_text(ocr_results)
        
        # Find best match
        best_match, similarity = find_best_match(user_name, potential_names, threshold)
        
        # Determine if auto-approval is possible
        auto_approved = similarity >= threshold
        
        # Prepare response
        result = {
            'verified': auto_approved,
            'auto_approved': auto_approved,
            'similarity_score': round(similarity, 4),
            'threshold': threshold,
            'user_name': user_name,
            'extracted_names': [p['name'] for p in potential_names],
            'best_match': best_match,
            'all_text_detected': [t['text'] for t in all_text[:20]],  # Limit for response size
            'message': ''
        }
        
        if auto_approved:
            result['message'] = f"ID verified successfully! Name match: {round(similarity * 100, 1)}%"
        elif potential_names:
            result['message'] = f"Name mismatch detected. Best match: {round(similarity * 100, 1)}%. Requires admin approval."
        else:
            result['message'] = "Could not extract name from ID card. Requires admin approval."
        
        print(f"Verification result for {user_name}: {result['message']}")
        return jsonify(result)
        
    except Exception as e:
        print(f"Error processing image: {e}")
        return jsonify({
            'error': str(e),
            'verified': False,
            'auto_approved': False,
            'message': 'Error processing ID card. Requires admin approval.'
        }), 500
        
    finally:
        # Cleanup temp file
        if temp_file and os.path.exists(temp_file.name):
            try:
                os.unlink(temp_file.name)
            except:
                pass

@app.route('/extract-text', methods=['POST'])
def extract_text():
    """
    Extract all text from an image (useful for debugging).
    
    Request body:
    {
        "image_base64": "base64_encoded_image_data",
        "image_path": "/path/to/image.jpg"  # Alternative to base64
    }
    """
    if reader is None:
        return jsonify({'error': 'OCR model not loaded'}), 500
    
    data = request.get_json()
    temp_file = None
    
    try:
        if 'image_base64' in data:
            image_data = base64.b64decode(data['image_base64'])
            image = Image.open(io.BytesIO(image_data))
            
            # Convert RGBA to RGB if necessary (JPEG doesn't support transparency)
            if image.mode == 'RGBA':
                background = Image.new('RGB', image.size, (255, 255, 255))
                background.paste(image, mask=image.split()[3])
                image = background
            elif image.mode != 'RGB':
                image = image.convert('RGB')
            
            temp_file = tempfile.NamedTemporaryFile(suffix='.jpg', delete=False)
            image.save(temp_file.name, 'JPEG', quality=95)
            image_path = temp_file.name
            
        elif 'image_path' in data:
            image_path = data['image_path']
        else:
            return jsonify({'error': 'Missing image_base64 or image_path'}), 400
        
        ocr_results = reader.readtext(image_path)
        
        extracted = [{
            'text': r[1],
            'confidence': round(r[2], 4),
            'bbox': [[int(p[0]), int(p[1])] for p in r[0]]
        } for r in ocr_results]
        
        return jsonify({
            'success': True,
            'text_count': len(extracted),
            'extracted_text': extracted
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500
        
    finally:
        if temp_file and os.path.exists(temp_file.name):
            try:
                os.unlink(temp_file.name)
            except:
                pass

if __name__ == '__main__':
    print("=" * 60)
    print("ID Verification API - OCR-based Name Verification")
    print("=" * 60)
    
    if load_ocr_reader():
        print("Starting ID Verification API on http://localhost:5004")
        print("Endpoints:")
        print("  GET  /health - Health check")
        print("  POST /verify - Verify name on ID card")
        print("  POST /extract-text - Extract all text from image")
        print("=" * 60)
        app.run(host='0.0.0.0', port=5004, debug=False)
    else:
        print("Failed to load OCR model. Exiting.")
        exit(1)
