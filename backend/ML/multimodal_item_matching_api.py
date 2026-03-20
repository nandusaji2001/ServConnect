"""
Enhanced Lost & Found Item Matching API
Multimodal (CLIP) + Graph Neural Network (GNN) for Trust Scoring
"""

import os
from flask import Flask, request, jsonify
from flask_cors import CORS
from multimodal_matching_service import MultimodalMatchingService
from datetime import datetime
import traceback

app = Flask(__name__)
CORS(app)

# Global service
matching_service = None

def load_service():
    """Initialize the multimodal matching service"""
    global matching_service
    try:
        print("="*60)
        print("Initializing Multimodal Item Matching Service")
        print("="*60)
        matching_service = MultimodalMatchingService(use_gat=False)  # Use GraphSAGE
        print("="*60)
        print("Service initialized successfully!")
        print("="*60)
        return True
    except Exception as e:
        print(f"Error loading service: {e}")
        traceback.print_exc()
        return False

@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'healthy',
        'service_loaded': matching_service is not None,
        'features': ['CLIP', 'GNN', 'Multimodal']
    })

@app.route('/match', methods=['POST'])
def match_items():
    """
    Find matching items using multimodal similarity and GNN trust scores
    
    Request body:
    {
        "query_item": {
            "id": "item_id",
            "title": "Black Leather Wallet",
            "category": "Wallet",
            "description": "Contains credit cards and ID",
            "location": "Central Park",
            "images": ["url1", "url2"]  // Optional
        },
        "candidate_items": [
            {
                "id": "lost_item_1",
                "user_id": "user_123",
                "title": "Lost Wallet",
                "category": "Wallet",
                "description": "Black wallet with cards",
                "location": "Park area",
                "images": ["url1"]  // Optional
            }
        ],
        "users": [  // Optional: for GNN trust scoring
            {
                "id": "user_123",
                "posts_count": 10,
                "reports_count": 0,
                "account_age_days": 365
            }
        ],
        "items_metadata": [  // Optional: for GNN trust scoring
            {
                "id": "lost_item_1",
                "user_id": "user_123",
                "claims_count": 2,
                "verified": true,
                "age_days": 5
            }
        ],
        "interactions": [  // Optional: for GNN
            {
                "user_id": "user_123",
                "item_id": "lost_item_1",
                "type": "claim"
            }
        ],
        "threshold": 0.5,  // Optional
        "top_k": 5,  // Optional
        "alpha": 0.7,  // Weight for similarity
        "beta": 0.3  // Weight for trust
    }
    
    Response:
    {
        "success": true,
        "matches": [
            {
                "item": {...},
                "similarity_score": 0.85,
                "trust_score": 0.75,
                "final_score": 0.82,
                "match_percentage": 82.0,
                "details": {
                    "image_similarity": 0.90,
                    "text_similarity": 0.80,
                    "cross_modal_similarity": 0.85,
                    "user_trust": 0.80,
                    "item_trust": 0.70
                }
            }
        ]
    }
    """
    if matching_service is None:
        return jsonify({'error': 'Service not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data:
        return jsonify({'error': 'No data provided', 'success': False}), 400
    
    query_item = data.get('query_item')
    candidate_items = data.get('candidate_items', [])
    threshold = data.get('threshold', 0.5)
    top_k = data.get('top_k', 5)
    alpha = data.get('alpha', 0.7)
    beta = data.get('beta', 0.3)
    
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
        # Initialize GNN if data provided
        user_trust_scores = None
        item_trust_scores = None
        
        users = data.get('users', [])
        items_metadata = data.get('items_metadata', [])
        interactions = data.get('interactions', [])
        
        if users and items_metadata:
            print("Initializing GNN for trust scoring...")
            user_trust_scores, item_trust_scores = matching_service.initialize_gnn(
                users, items_metadata, interactions
            )
            print(f"GNN initialized: {len(user_trust_scores)} users, {len(item_trust_scores)} items")
        
        # Find matches
        matches = matching_service.find_matches(
            query_item=query_item,
            candidate_items=candidate_items,
            user_trust_scores=user_trust_scores,
            item_trust_scores=item_trust_scores,
            threshold=threshold,
            top_k=top_k,
            alpha=alpha,
            beta=beta
        )
        
        return jsonify({
            'success': True,
            'matches': matches,
            'query_item_id': query_item.get('id'),
            'total_candidates': len(candidate_items),
            'matches_found': len(matches),
            'gnn_enabled': user_trust_scores is not None
        })
    
    except Exception as e:
        print(f"Error in match endpoint: {e}")
        traceback.print_exc()
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/similarity', methods=['POST'])
def compute_similarity():
    """
    Compute multimodal similarity between two items
    
    Request body:
    {
        "item1": {
            "title": "...",
            "category": "...",
            "description": "...",
            "location": "...",
            "images": ["url1"]  // Optional
        },
        "item2": {...}
    }
    
    Response:
    {
        "success": true,
        "similarity_score": 0.85,
        "details": {
            "image_similarity": 0.90,
            "text_similarity": 0.80,
            "cross_modal_similarity": 0.85
        }
    }
    """
    if matching_service is None:
        return jsonify({'error': 'Service not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data or 'item1' not in data or 'item2' not in data:
        return jsonify({'error': 'Missing item1 or item2', 'success': False}), 400
    
    try:
        similarities = matching_service.compute_multimodal_similarity(
            data['item1'], data['item2']
        )
        
        return jsonify({
            'success': True,
            'similarity_score': similarities['combined_sim'],
            'match_percentage': round(similarities['combined_sim'] * 100, 1),
            'details': {
                'image_similarity': similarities['image_sim'],
                'text_similarity': similarities['text_sim'],
                'cross_modal_similarity': similarities['cross_modal_sim']
            }
        })
    
    except Exception as e:
        print(f"Error computing similarity: {e}")
        traceback.print_exc()
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/trust_scores', methods=['POST'])
def compute_trust_scores():
    """
    Compute GNN trust scores for users and items
    
    Request body:
    {
        "users": [
            {
                "id": "user_123",
                "posts_count": 10,
                "reports_count": 0,
                "account_age_days": 365
            }
        ],
        "items": [
            {
                "id": "item_1",
                "user_id": "user_123",
                "claims_count": 2,
                "verified": true,
                "age_days": 5
            }
        ],
        "interactions": [
            {
                "user_id": "user_123",
                "item_id": "item_1",
                "type": "claim"
            }
        ]
    }
    
    Response:
    {
        "success": true,
        "user_trust_scores": {
            "user_123": 0.85
        },
        "item_trust_scores": {
            "item_1": 0.75
        }
    }
    """
    if matching_service is None:
        return jsonify({'error': 'Service not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data:
        return jsonify({'error': 'No data provided', 'success': False}), 400
    
    users = data.get('users', [])
    items = data.get('items', [])
    interactions = data.get('interactions', [])
    
    if not users or not items:
        return jsonify({'error': 'Missing users or items', 'success': False}), 400
    
    try:
        user_trust_scores, item_trust_scores = matching_service.initialize_gnn(
            users, items, interactions
        )
        
        return jsonify({
            'success': True,
            'user_trust_scores': user_trust_scores,
            'item_trust_scores': item_trust_scores,
            'total_users': len(user_trust_scores),
            'total_items': len(item_trust_scores)
        })
    
    except Exception as e:
        print(f"Error computing trust scores: {e}")
        traceback.print_exc()
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/embed/image', methods=['POST'])
def embed_image():
    """
    Get CLIP embedding for an image
    
    Request body:
    {
        "image_url": "http://..."
    }
    
    Response:
    {
        "success": true,
        "embedding": [0.1, 0.2, ...],
        "dimension": 512
    }
    """
    if matching_service is None:
        return jsonify({'error': 'Service not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data or 'image_url' not in data:
        return jsonify({'error': 'Missing image_url', 'success': False}), 400
    
    try:
        embedding = matching_service.clip_service.encode_image(data['image_url'])
        
        return jsonify({
            'success': True,
            'embedding': embedding.tolist(),
            'dimension': len(embedding)
        })
    
    except Exception as e:
        print(f"Error embedding image: {e}")
        return jsonify({'error': str(e), 'success': False}), 500

@app.route('/embed/text', methods=['POST'])
def embed_text():
    """
    Get CLIP embedding for text
    
    Request body:
    {
        "text": "Black leather wallet"
    }
    
    Response:
    {
        "success": true,
        "embedding": [0.1, 0.2, ...],
        "dimension": 512
    }
    """
    if matching_service is None:
        return jsonify({'error': 'Service not loaded', 'success': False}), 500
    
    data = request.get_json()
    
    if not data or 'text' not in data:
        return jsonify({'error': 'Missing text', 'success': False}), 400
    
    try:
        embedding = matching_service.clip_service.encode_text(data['text'])
        
        return jsonify({
            'success': True,
            'embedding': embedding.tolist(),
            'dimension': len(embedding)
        })
    
    except Exception as e:
        print(f"Error embedding text: {e}")
        return jsonify({'error': str(e), 'success': False}), 500

if __name__ == '__main__':
    if load_service():
        port = int(os.environ.get('MULTIMODAL_MATCHING_PORT', 5003))
        print(f"\n{'='*60}")
        print(f"Starting Multimodal Item Matching API on port {port}")
        print(f"{'='*60}\n")
        app.run(host='0.0.0.0', port=port, debug=False)
    else:
        print("Failed to load multimodal matching service.")
        print("Please check your installation and dependencies.")
