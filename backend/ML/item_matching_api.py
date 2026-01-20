"""
Lost & Found Item Matching API
Uses Sentence-BERT (S-BERT) for semantic similarity matching between lost and found items.
When a new found item is reported, it checks for similar lost item reports and notifies owners.
"""

import os
from flask import Flask, request, jsonify
from flask_cors import CORS
from sentence_transformers import SentenceTransformer
from sklearn.metrics.pairwise import cosine_similarity
import numpy as np

app = Flask(__name__)
CORS(app)

# Global model
model = None
MODEL_NAME = 'all-MiniLM-L6-v2'  # Lightweight but effective S-BERT model

def load_model():
    """Load the S-BERT model"""
    global model
    try:
        print(f"Loading S-BERT model: {MODEL_NAME}...")
        model = SentenceTransformer(MODEL_NAME)
        print("S-BERT model loaded successfully!")
        return True
    except Exception as e:
        print(f"Error loading model: {e}")
        return False

def create_item_text(item):
    """Create a combined text representation of an item for embedding"""
    parts = []
    
    if item.get('title'):
        parts.append(item['title'])
    
    if item.get('category'):
        parts.append(f"Category: {item['category']}")
    
    if item.get('description'):
        parts.append(item['description'])
    
    if item.get('location'):
        parts.append(f"Location: {item['location']}")
    
    return ' '.join(parts)

def compute_similarity(text1, text2):
    """Compute semantic similarity between two texts using S-BERT"""
    if model is None:
        return 0.0
    
    embeddings = model.encode([text1, text2])
    similarity = cosine_similarity([embeddings[0]], [embeddings[1]])[0][0]
    return float(similarity)

def find_matches(query_item, candidate_items, threshold=0.5, top_k=5):
    """
    Find matching items from candidates based on semantic similarity.
    
    Args:
        query_item: The item to match (dict with title, category, description, location)
        candidate_items: List of candidate items to search through
        threshold: Minimum similarity score (0-1) to consider a match
        top_k: Maximum number of matches to return
    
    Returns:
        List of matching items with similarity scores
    """
    if model is None or not candidate_items:
        return []
    
    query_text = create_item_text(query_item)
    query_embedding = model.encode([query_text])[0]
    
    matches = []
    
    for candidate in candidate_items:
        candidate_text = create_item_text(candidate)
        candidate_embedding = model.encode([candidate_text])[0]
        
        similarity = cosine_similarity([query_embedding], [candidate_embedding])[0][0]
        
        # Category boost: if categories match, boost similarity
        if query_item.get('category') and candidate.get('category'):
            if query_item['category'].lower() == candidate['category'].lower():
                similarity = min(1.0, similarity + 0.15)  # Boost by 15%
        
        if similarity >= threshold:
            matches.append({
                'item': candidate,
                'similarity': float(similarity),
                'match_percentage': round(float(similarity) * 100, 1)
            })
    
    # Sort by similarity descending and return top_k
    matches.sort(key=lambda x: x['similarity'], reverse=True)
    return matches[:top_k]

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'model_loaded': model is not None,
        'model_name': MODEL_NAME
    })

@app.route('/match', methods=['POST'])
def match_items():
    """
    Find matching items between a query item and candidate items.
    
    Request body:
    {
        "query_item": {
            "id": "item_id",
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
            },
            ...
        ],
        "threshold": 0.5,  # optional, default 0.5
        "top_k": 5  # optional, default 5
    }
    
    Response:
    {
        "success": true,
        "matches": [
            {
                "item": {...},
                "similarity": 0.85,
                "match_percentage": 85.0
            },
            ...
        ],
        "query_item_id": "item_id"
    }
    """
    if model is None:
        return jsonify({'error': 'Model not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data:
        return jsonify({'error': 'No data provided', 'success': False}), 400
    
    query_item = data.get('query_item')
    candidate_items = data.get('candidate_items', [])
    threshold = data.get('threshold', 0.5)
    top_k = data.get('top_k', 5)
    
    if not query_item:
        return jsonify({'error': 'Missing query_item', 'success': False}), 400
    
    if not candidate_items:
        return jsonify({
            'success': True,
            'matches': [],
            'query_item_id': query_item.get('id'),
            'message': 'No candidate items to match against'
        })
    
    try:
        matches = find_matches(query_item, candidate_items, threshold, top_k)
        
        return jsonify({
            'success': True,
            'matches': matches,
            'query_item_id': query_item.get('id'),
            'total_candidates': len(candidate_items),
            'matches_found': len(matches)
        })
    except Exception as e:
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/similarity', methods=['POST'])
def compute_similarity_endpoint():
    """
    Compute similarity between two items.
    
    Request body:
    {
        "item1": {
            "title": "...",
            "category": "...",
            "description": "...",
            "location": "..."
        },
        "item2": {
            "title": "...",
            "category": "...",
            "description": "...",
            "location": "..."
        }
    }
    
    Response:
    {
        "success": true,
        "similarity": 0.85,
        "match_percentage": 85.0
    }
    """
    if model is None:
        return jsonify({'error': 'Model not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data or 'item1' not in data or 'item2' not in data:
        return jsonify({'error': 'Missing item1 or item2', 'success': False}), 400
    
    try:
        text1 = create_item_text(data['item1'])
        text2 = create_item_text(data['item2'])
        
        similarity = compute_similarity(text1, text2)
        
        # Category boost
        if data['item1'].get('category') and data['item2'].get('category'):
            if data['item1']['category'].lower() == data['item2']['category'].lower():
                similarity = min(1.0, similarity + 0.15)
        
        return jsonify({
            'success': True,
            'similarity': similarity,
            'match_percentage': round(similarity * 100, 1)
        })
    except Exception as e:
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/embed', methods=['POST'])
def get_embedding():
    """
    Get the embedding vector for an item (useful for caching).
    
    Request body:
    {
        "item": {
            "title": "...",
            "category": "...",
            "description": "...",
            "location": "..."
        }
    }
    
    Response:
    {
        "success": true,
        "embedding": [0.1, 0.2, ...],
        "dimension": 384
    }
    """
    if model is None:
        return jsonify({'error': 'Model not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data or 'item' not in data:
        return jsonify({'error': 'Missing item', 'success': False}), 400
    
    try:
        text = create_item_text(data['item'])
        embedding = model.encode([text])[0]
        
        return jsonify({
            'success': True,
            'embedding': embedding.tolist(),
            'dimension': len(embedding)
        })
    except Exception as e:
        return jsonify({'error': str(e), 'success': False}), 500

if __name__ == '__main__':
    if load_model():
        port = int(os.environ.get('ITEM_MATCHING_PORT', 5003))
        print(f"Starting Item Matching API on port {port}...")
        app.run(host='0.0.0.0', port=port, debug=False)
    else:
        print("Failed to load S-BERT model. Please check your installation.")
        print("Install with: pip install sentence-transformers")
